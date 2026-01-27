import base64
import json
from io import BytesIO
import os
import glob
import asyncio
from typing import Any, Dict, Optional

import numpy as np
import torch
from PIL import Image

import websockets

from model import *


def _weights_path() -> str:
    # server/ is the working directory when you run python server.py
    return os.path.join(os.path.dirname(__file__), "mnist_cnn.pth")


def _unity_pics_dir() -> str:
    # Unity project path: <repo>/unity/cnn_visualizer/Assets/Pics
    repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    return os.path.join(repo_root, "unity", "cnn_visualizer", "Assets", "Pics")


def _latest_png_path(pics_dir: str) -> str:
    patterns = [os.path.join(pics_dir, "*.png"), os.path.join(pics_dir, "*.PNG")]
    candidates: list[str] = []
    for p in patterns:
        candidates.extend(glob.glob(p))
    if not candidates:
        raise FileNotFoundError(f"No PNG images found in: {pics_dir}")
    return max(candidates, key=os.path.getmtime)


def get_input_tensor_from_local_png(png_path: str):
    """Load a local PNG (Unity output), transform to MNIST tensor, and return tensor + raw bytes."""
    with open(png_path, "rb") as f:
        image_bytes = f.read()

    image = Image.open(BytesIO(image_bytes)).convert("RGB")
    input_tensor = transform(image).unsqueeze(0)  # [1,1,28,28]
    return input_tensor, image_bytes


def _encode_f32_base64(array: np.ndarray) -> str:
    """Encode a numpy float32 array as base64 (little-endian)"""
    return base64.b64encode(array.astype(np.float32).tobytes()).decode("utf-8")


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
    """Reshape a 1D vector into a 2D grid for visualization (pads with zeros)."""
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
    try:
        cnn.load_weights(path=_weights_path())
        print("✅ Model weights loaded")
    except Exception as e:
        print(f"⚠️ Could not load weights: {e}")
    cnn.eval()

    # Pre-compute a compact FC graph representation once (weights are static).
    # We aggregate fc1 weights from 12544 inputs into 64 channels by grouping 14*14 per channel.
    fc_graph = None
    try:
        with torch.no_grad():
            fc1_w = cnn.fc1.weight.detach().cpu().numpy()  # [128, 64*14*14]
            fc2_w = cnn.fc2.weight.detach().cpu().numpy()  # [10, 128]

        if fc1_w.shape[1] == 64 * 14 * 14 and fc2_w.shape == (10, 128):
            fc1_w = fc1_w.reshape(128, 64, 14 * 14)
            # Magnitude aggregation per conv2 channel -> fc1 neuron
            w1 = np.mean(np.abs(fc1_w), axis=2).astype(np.float32)  # [128, 64]
            w2 = np.abs(fc2_w).astype(np.float32)  # [10, 128]

            fc_graph = {
                "sizes": [64, 128, 10],
                "w1": {
                    "shape": [128, 64],
                    "dtype": "float32",
                    "aggregation": "mean_abs_over_14x14",
                    "base64": _encode_f32_base64(w1),
                    "min": float(w1.min()),
                    "max": float(w1.max()),
                },
                "w2": {
                    "shape": [10, 128],
                    "dtype": "float32",
                    "aggregation": "abs",
                    "base64": _encode_f32_base64(w2),
                    "min": float(w2.min()),
                    "max": float(w2.max()),
                },
            }
            print("✅ FC graph weights prepared (64->128->10)")
        else:
            print(f"⚠️ FC weight shapes unexpected: fc1={fc1_w.shape}, fc2={fc2_w.shape}")
    except Exception as e:
        print(f"⚠️ Could not prepare FC graph weights: {e}")
    
    while True:
        try:
            # Get input image tensor from the latest Unity-exported PNG
            pics_dir = _unity_pics_dir()
            png_path = _latest_png_path(pics_dir)
            input_tensor, image_bytes = get_input_tensor_from_local_png(png_path)
            print(f"📨 Got input tensor shape: {input_tensor.shape}")
            
            # 🎯 Register hooks to capture activations
            activations = {}
            hook_handles = {}
            
            def get_hook(name):
                def hook(module, input, output):
                    activations[name] = output.detach()
                return hook
            
            # Register hooks on all layers we want to visualize
            for name, module in cnn.named_modules():
                if isinstance(module, (torch.nn.Conv2d, torch.nn.MaxPool2d)):
                    hook_handles[name] = module.register_forward_hook(get_hook(name))
            
            # Make prediction (this triggers the hooks)
            with torch.no_grad():
                output = cnn(input_tensor)
                predicted = torch.argmax(output, dim=1)
                print(f"🤖 Predicted class: {predicted.item()}")
            
            # Remove hooks
            for handle in hook_handles.values():
                handle.remove()
            
            # Sort activations
            activations = dict(sorted(activations.items()))
            print(f"📊 Got {len(activations)} layer activations")

            input_image_base64 = base64.b64encode(image_bytes).decode("utf-8")

            # 🎯 Build feature_maps in the correct format
            feature_maps = {}
            for layer_idx, (layer_name, activation) in enumerate(activations.items()):
                try:
                    act = activation.squeeze(0)  # Remove batch dimension

                    layer_key = layer_name.split('.')[0] if '.' in layer_name else layer_name
                    if layer_key not in feature_maps:
                        feature_maps[layer_key] = {}

                    # Conv output: [C,H,W] -> export ALL channels
                    if len(act.shape) == 3:
                        act_np = act.detach().cpu().numpy()  # [C,H,W]
                        c, h, w = act_np.shape
                        for ch in range(c):
                            fmap2d = act_np[ch]
                            feature_maps[layer_key][f"ch_{ch:03d}"] = {
                                "shape": [int(h), int(w)],
                                "base64": _png_base64_from_2d(fmap2d),
                            }

                    # FC output: [N] -> export ONE reshaped map
                    elif len(act.shape) == 1:
                        vec = act.detach().cpu().numpy().astype(np.float32)
                        if layer_key == "fc1":
                            fmap2d = _vector_to_2d(vec, prefer_rows=16, prefer_cols=8)  # 128
                        elif layer_key == "fc2":
                            fmap2d = _vector_to_2d(vec, prefer_rows=10, prefer_cols=1)  # 10
                        else:
                            fmap2d = _vector_to_2d(vec)

                        h, w = fmap2d.shape
                        feature_maps[layer_key]["vector"] = {
                            "shape": [int(h), int(w)],
                            "base64": _png_base64_from_2d(fmap2d),
                        }

                    else:
                        continue
                    
                except Exception as e:
                    print(f"⚠️ Error processing layer {layer_name}: {e}")
                    continue

            final_data = {
                "input_image_base64": input_image_base64,
                "input_image_shape": [28, 28],
                "feature_maps": feature_maps,
            }

            if fc_graph is not None:
                final_data["fc_graph"] = fc_graph

            # Save debug info
            try:
                with open("activations.json", "w") as f:
                    debug_data = {
                        "input_image_shape": final_data["input_image_shape"],
                        "feature_maps": {
                            k: {
                                fk: {"shape": v["shape"]} for fk, v in filters.items()
                            }
                            for k, filters in feature_maps.items()
                        }
                    }
                    json.dump(debug_data, f, indent=4)
            except:
                pass

            total_maps = sum(len(v) for v in feature_maps.values())
            print(f"✅ Sending {len(feature_maps)} layers / {total_maps} feature maps")
            await websocket.send(json.dumps(final_data))
            await asyncio.sleep(30.0)

        except websockets.exceptions.ConnectionClosed:
            print("🔌 Unity disconnected.")
            break
        except Exception as e:
            print(f"❌ Error in handler: {e}")
            import traceback
            traceback.print_exc()
            break


if __name__ == "__main__":
    # Run a websocket server for Unity.
    port = int(os.environ.get("PORT", "8765"))
    async def _main():
        async with websockets.serve(handler, "0.0.0.0", port):
            print(f"✅ WebSocket server running on ws://0.0.0.0:{port}")
            await asyncio.Future()  # run forever

    asyncio.run(_main())