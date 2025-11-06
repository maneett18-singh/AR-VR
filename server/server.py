import asyncio
import json
import torch
import websockets
from typing import Any
from model import model, get_activations, make_prediction, get_input_tensor, get_cnn_summary, tensor_to_base64, tensor_to_base64, show_feature_maps

async def handler(websocket):
    print("Unity connected!")

    # Load model once
    cnn = model()
    cnn.load_weights("mnist_cnn.pth")
    cnn.eval()

    # Register activations hooks once
    activations = get_activations(cnn)

    while True:
        try:
            # Get input image tensor
            image_url = "https://conx.readthedocs.io/en/latest/_images/MNIST_6_0.png"
            input_tensor, _ = get_input_tensor(image_url)

            # Make prediction
            predicted_class = make_prediction(cnn, input_tensor)

            data: dict[str, Any] = {"predicted_class": predicted_class}

            for idx, (layer_name, values) in enumerate(activations.items()):
                print("layer name:", layer_name)
                # 4D tensor: [batch, channels, H, W]
                tensor_shape = tuple(values.squeeze(0).shape)
                logits = values.squeeze(0).mean(dim=0)  # mean across channels
                data[f"layer_{idx+1}"] = {
                    "name": layer_name,
                    "shape": tensor_shape,
                    "data": logits.cpu().detach().tolist()
                }
            
            cnn_summary = get_cnn_summary(cnn, input_tensor)
            data["cnn_summary"] = cnn_summary
            data["input_image_base64"] = tensor_to_base64(input_tensor)
            data["input_image_shape"] = tuple(input_tensor.squeeze().shape)
            data["feature_maps"] = {}

            # for layer_name, values in activations.items():
            #     if values.ndim == 4:  # Only 4D tensors have feature maps
            #         maps = []
            #         for i in range(values.shape[1]):  # loop over channels
            #             maps.append(tensor_to_base64(values[0, i]))  # convert each feature map to base64
            #         data["feature_maps"][layer_name] = maps
            
            for layer_name, values in activations.items():
                if values.ndim == 4:  # Only Conv/Pool layers
                    maps = {}  # dictionary instead of list
                    for i in range(values.shape[1]):  # loop over channels
                        fmap_tensor = values[0, i]  # [H, W]
                        maps[i + 1] = {  # use map index as key
                            "shape": tuple(fmap_tensor.shape),
                            "base64": tensor_to_base64(fmap_tensor)
            }
                    data["feature_maps"][layer_name] = maps  # nested dict


            # for layer_name, values in activations.items():
            #     if values.ndim == 4:  # Conv/Pool layers
            #         maps = []
            #         for i in range(values.shape[1]):  # loop over channels        
            #             fmap_tensor = values[0, i]  # [H, W]
            #             fmap_info = {
            #                 "map_index": i + 1,
            #                 "shape": tuple(fmap_tensor.shape),
            #                 "base64": tensor_to_base64(values[0, i])
            #             }
            #             maps.append(fmap_info)
            #         data["feature_maps"][layer_name] = maps


            # Input image
            data["input_tensor"] = input_tensor.squeeze().cpu().detach().tolist()

            # Save JSON
            with open("activations.json", "w") as f:
                json.dump(data, f, indent=4)

            # Send to Unity
            await websocket.send(json.dumps(data))
            await asyncio.sleep(1.0)

        except websockets.exceptions.ConnectionClosed:
            print("Unity disconnected. Exiting handler.")
            break
        except Exception as e:
            print(f"Error in handler: {e}")
            break

async def main():
    async with websockets.serve(handler, "0.0.0.0", 8765):
        print("WebSocket server running on ws://localhost:8765")
        await asyncio.Future()  # run forever

if __name__ == "__main__":
    asyncio.run(main())
