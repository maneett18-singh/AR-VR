import asyncio
import json
import torch
import websockets
from typing import Any
from model import (
    model,
    get_activations,
    make_prediction,
    get_input_tensor,
    get_cnn_summary,
    tensor_to_base64,
)


async def handler(websocket):
    print("✅ Unity connected!")

    # Load model once
    cnn = model()
    cnn.load_weights("mnist_cnn.pth")
    cnn.eval()

    # Register activation hooks
    activations = get_activations(cnn)

    while True:
        try:
            # Example image
            image_url = "https://conx.readthedocs.io/en/latest/_images/MNIST_6_0.png"
            input_tensor, _ = get_input_tensor(image_url)

            # Forward pass
            predicted_class = make_prediction(cnn, input_tensor)

            # Base data
            data: dict[str, Any] = {
                "predicted_class": int(predicted_class),
                "cnn_summary": get_cnn_summary(cnn, input_tensor),
                "input_image_base64": tensor_to_base64(input_tensor),
                "input_image_shape": tuple(input_tensor.squeeze().shape),
                "feature_maps": {}
            }

            # Include limited feature maps (avoid huge JSON)
            for layer_name, values in activations.items():
                if values.ndim == 4:
                    maps = {}
                    for i in range(min(values.shape[1], 3)):  # only 3 maps per layer
                        fmap_tensor = values[0, i]
                        maps[str(i + 1)] = {
                            "shape": tuple(fmap_tensor.shape),
                            "base64": tensor_to_base64(fmap_tensor)
                        }
                    data["feature_maps"][layer_name] = maps

            # Add a simple numeric tensor summary
            data["input_tensor_mean"] = float(input_tensor.mean().item())

            # Convert to JSON
            json_data = json.dumps(data, indent=4, ensure_ascii=False)

            # ✅ Save to file for debugging/inspection
            with open("newdata.json", "w", encoding="utf-8") as f:
                f.write(json_data)

            # ✅ Send to Unity
            await websocket.send(json_data)

            print("📤 Sent data to Unity and saved as activations.json")
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
