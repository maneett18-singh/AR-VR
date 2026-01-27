import base64
import json
from dataclasses import dataclass
from io import BytesIO
import os
from typing import Any, Dict, Optional

import numpy as np
import torch
import torch.nn.functional as F
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from PIL import Image

from model import *

# -----------------------------------------------------------------------------
# Single-shot server (no WebSockets)
#
# Contract:
# - POST /infer  with either:
#     {"image_url": "http(s)://..."}
#   or
#     {"image_base64": "..."}
# - Returns ONE JSON response (same shape Unity visualizer expects):
#     {"input_image_base64": "...", "input_image_shape": [28,28], "feature_maps": {...}}
# -----------------------------------------------------------------------------

app = FastAPI(title="CNN Feature Map Server", version="1.0")


class InferRequest(BaseModel):
    image_url: Optional[str] = None
    image_base64: Optional[str] = None


@dataclass
class ModelState:
    cnn: Any


STATE: Optional[ModelState] = None


def _weights_path() -> str:
    # Always resolve relative to this file so running from any CWD works.
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(here, "mnist_cnn.pth")


def _load_model() -> ModelState:
    cnn = model()
    try:
        cnn.load_weights(path=_weights_path())
        print("✅ Model weights loaded")
    except Exception as e:
        print(f"⚠️ Could not load weights: {e}")
    cnn.eval()
    return ModelState(cnn=cnn)


def _build_feature_payload(cnn, input_tensor: torch.Tensor, image_bytes: bytes) -> Dict[str, Any]:
    activations: Dict[str, torch.Tensor] = {}
    hook_handles: Dict[str, Any] = {}

    def get_hook(name):
        def hook(module, _input, output):
            activations[name] = output.detach()
        return hook

    for name, module in cnn.named_modules():
        if isinstance(module, (torch.nn.Conv2d, torch.nn.Linear)):
            hook_handles[name] = module.register_forward_hook(get_hook(name))

    with torch.no_grad():
        output = cnn(input_tensor)
        predicted = torch.argmax(output, dim=1)
        print(f"🤖 Predicted class: {predicted.item()}")

    for handle in hook_handles.values():
        handle.remove()

    activations = dict(sorted(activations.items()))
    print(f"📊 Got {len(activations)} layer activations")

    input_image_base64 = base64.b64encode(image_bytes).decode("utf-8")

    feature_maps: Dict[str, Dict[str, Dict[str, Any]]] = {}
    for layer_name, activation in activations.items():
        try:
            act = activation.squeeze(0)

            # Conv output: [C,H,W] -> store each channel as PNG
            if len(act.shape) == 3:
                for ch in range(act.shape[0]):
                    act_2d = act[ch]
                    act_2d_small = F.interpolate(
                        act_2d.unsqueeze(0).unsqueeze(0),
                        size=(14, 14),
                        mode="bilinear",
                        align_corners=False,
                    ).squeeze(0).squeeze(0)

                    act_np = act_2d_small.cpu().numpy()
                    act_min, act_max = act_np.min(), act_np.max()
                    if act_max > act_min:
                        act_normalized = ((act_np - act_min) / (act_max - act_min) * 255).astype(np.uint8)
                    else:
                        act_normalized = (act_np * 255).astype(np.uint8)

                    activation_img = Image.fromarray(act_normalized)
                    img_buffer = BytesIO()
                    activation_img.save(img_buffer, format="PNG")
                    activation_base64 = base64.b64encode(img_buffer.getvalue()).decode("utf-8")

                    layer_key = layer_name.split(".")[0] if "." in layer_name else layer_name
                    if layer_key not in feature_maps:
                        feature_maps[layer_key] = {}

                    filter_name = f"filter_{len(feature_maps[layer_key])}"
                    feature_maps[layer_key][filter_name] = {
                        "shape": list(act_2d_small.shape),
                        "base64": activation_base64,
                    }
                continue

            # FC output: [N] -> reshape to square-ish
            if len(act.shape) == 1:
                size = int(np.sqrt(act.shape[0]))
                act_2d = act[: size * size].view(size, size)
            else:
                continue

            act_np = act_2d.cpu().numpy()
            act_min, act_max = act_np.min(), act_np.max()
            if act_max > act_min:
                act_normalized = ((act_np - act_min) / (act_max - act_min) * 255).astype(np.uint8)
            else:
                act_normalized = (act_np * 255).astype(np.uint8)

            activation_img = Image.fromarray(act_normalized)
            img_buffer = BytesIO()
            activation_img.save(img_buffer, format="PNG")
            activation_base64 = base64.b64encode(img_buffer.getvalue()).decode("utf-8")

            layer_key = layer_name.split(".")[0] if "." in layer_name else layer_name
            if layer_key not in feature_maps:
                feature_maps[layer_key] = {}

            filter_name = f"filter_{len(feature_maps[layer_key])}"
            feature_maps[layer_key][filter_name] = {
                "shape": list(act_2d.shape),
                "base64": activation_base64,
            }

        except Exception as e:
            print(f"⚠️ Error processing layer {layer_name}: {e}")
            continue

    final_data = {
        "input_image_base64": input_image_base64,
        "input_image_shape": [28, 28],
        "feature_maps": feature_maps,
    }

    # Save a lightweight debug file (shapes only)
    try:
        with open("activations.json", "w") as f:
            debug_data = {
                "input_image_shape": final_data["input_image_shape"],
                "feature_maps": {
                    k: {fk: {"shape": v["shape"]} for fk, v in filters.items()}
                    for k, filters in feature_maps.items()
                },
            }
            json.dump(debug_data, f, indent=4)
    except Exception:
        pass

    return final_data


@app.on_event("startup")
def on_startup() -> None:
    # NOTE: on_event is deprecated in newer FastAPI versions, but it's still supported.
    # If you want to remove the warning entirely, we can migrate to lifespan.
    global STATE
    STATE = _load_model()


@app.get("/health")
def health() -> Dict[str, str]:
    return {"status": "ok"}


@app.post("/infer")
def infer(req: InferRequest) -> Dict[str, Any]:
    if STATE is None:
        raise HTTPException(status_code=500, detail="Model not initialized")

    if not req.image_url and not req.image_base64:
        raise HTTPException(status_code=400, detail="Provide either image_url or image_base64")

    try:
        if req.image_url:
            input_tensor, image_bytes = get_input_tensor(req.image_url)
        else:
            # image_base64 should be raw image bytes (png/jpg). Decode and adapt to your get_input_tensor behavior.
            image_bytes = base64.b64decode(req.image_base64)
            img = Image.open(BytesIO(image_bytes)).convert("L")
            # Minimal preprocessing: resize to 28x28 and normalize to match MNIST-ish input.
            img = img.resize((28, 28))
            arr = np.array(img, dtype=np.float32) / 255.0
            input_tensor = torch.from_numpy(arr).unsqueeze(0).unsqueeze(0)

        print(f"📨 Got input tensor shape: {tuple(input_tensor.shape)}")
        return _build_feature_payload(STATE.cnn, input_tensor, image_bytes)

    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


if __name__ == "__main__":
    # Run with: python server.py
    # (FastAPI uses uvicorn)
    import uvicorn

    port = int(os.environ.get("PORT", "8765"))
    uvicorn.run(app, host="0.0.0.0", port=port)