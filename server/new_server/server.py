import asyncio
import json
import torch
import websockets
from typing import Any, Optional
import numpy as np
import base64
from io import BytesIO
from PIL import Image
from model import (
    model,
    make_prediction,
    get_input_tensor,
    get_cnn_summary,
    tensor_to_base64,
)


def _normalize_to_uint8(arr: np.ndarray) -> np.ndarray:
    arr = arr.astype(np.float32)
    mn = float(np.min(arr))
    mx = float(np.max(arr))
    if mx > mn:
        out = (arr - mn) / (mx - mn)
    else:
        out = np.zeros_like(arr, dtype=np.float32)
    return (out * 255.0).clip(0, 255).astype(np.uint8)


def _png_base64_from_2d(arr2d: np.ndarray) -> str:
    img = Image.fromarray(_normalize_to_uint8(arr2d))
    buf = BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("utf-8")


def _vector_to_2d(arr1d: np.ndarray, prefer_rows: Optional[int] = None, prefer_cols: Optional[int] = None) -> np.ndarray:
    n = int(arr1d.shape[0])
    if prefer_rows is not None and prefer_cols is not None and prefer_rows * prefer_cols >= n:
        rows, cols = int(prefer_rows), int(prefer_cols)
    else:
        cols = int(np.ceil(np.sqrt(n)))
        rows = int(np.ceil(n / cols))

    out = np.zeros((rows, cols), dtype=np.float32)
    flat = arr1d.astype(np.float32).reshape(-1)
    out.reshape(-1)[:n] = flat[:n]
    return out


async def handler(websocket):
    print("✅ Unity connected!")

    # Load model once
    cnn = model()
    cnn.load_weights("mnist_cnn.pth")
    cnn.eval()

    while True:
        try:
            # Example image
            image_url = "https://conx.readthedocs.io/en/latest/_images/MNIST_6_0.png"
            input_tensor, _ = get_input_tensor(image_url)

            # Capture activations for model architecture layers
            activations = {}
            hook_handles = {}

            def get_hook(name):
                def hook(module, input, output):
                    activations[name] = output.detach()
                return hook

            for name, module in cnn.named_modules():
                if isinstance(module, (torch.nn.Conv2d, torch.nn.MaxPool2d)):
                    hook_handles[name] = module.register_forward_hook(get_hook(name))

            # Forward pass (hooks populate activations)
            predicted_class = make_prediction(cnn, input_tensor)

            for handle in hook_handles.values():
                handle.remove()

            # Base data
            data: dict[str, Any] = {
                "predicted_class": int(predicted_class),
                "cnn_summary": get_cnn_summary(cnn, input_tensor),
                "input_image_base64": tensor_to_base64(input_tensor),
                "input_image_shape": tuple(input_tensor.squeeze().shape),
                "feature_maps": {}
            }

            # Include ALL feature maps per layer
            for layer_name, values in sorted(activations.items()):
                # Conv/Pool outputs: [1, C, H, W]
                if values.ndim == 4:
                    act = values[0].cpu().numpy()  # [C,H,W]
                    c, h, w = act.shape
                    maps = {}
                    for ch in range(c):
                        maps[f"ch_{ch:03d}"] = {
                            "shape": [int(h), int(w)],
                            "base64": _png_base64_from_2d(act[ch]),
                        }
                    data["feature_maps"][layer_name] = maps

                # FC outputs are intentionally not included (only conv1->conv2->pool)

            # Add a simple numeric tensor summary
            data["input_tensor_mean"] = float(input_tensor.mean().item())

            # Convert to JSON
            json_data = json.dumps(data, ensure_ascii=False)

            # ✅ Save to file for debugging/inspection
            with open("newdata.json", "w", encoding="utf-8") as f:
                f.write(json_data)

            # ✅ Send to Unity
            await websocket.send(json_data)

            total_maps = sum(len(v) for v in data["feature_maps"].values())
            print(f"📤 Sent {len(data['feature_maps'])} layers / {total_maps} maps to Unity")
            await asyncio.sleep(1.0)

        except websockets.exceptions.ConnectionClosed:
            print("❌ Unity disconnected. Exiting handler.")
            break
        except Exception as e:
            print(f"⚠ Error in handler: {e}")
            break


async def main():
    async with websockets.serve(handler, "0.0.0.0", 8765):
        print("🚀 WebSocket server running on ws://localhost:8765")
        await asyncio.Future()  # run forever


if __name__ == "__main__":
    asyncio.run(main())
