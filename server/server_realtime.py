import asyncio
import json
import torch
import torch.nn.functional as F
import websockets
import numpy as np
from model import *

async def handler(websocket):
    print("Unity connected!")

    # Load model once
    cnn = model()
    cnn.load_weights(path="mnist_cnn.pth")
    while True:
        try:
            # Get input image tensor
            image_url = "https://machinelearningmastery.com/wp-content/uploads/2019/02/sample_image-300x298.png"
            input_tensor, image_bytes = get_input_tensor(image_url)
            # register hooks to get activations
            activations = get_activations(cnn)


            # Make prediction (using tensor)
            make_prediction(cnn,image_url)
            # sorting the activations
            activations = dict(sorted(activations.items()))


            data = {}
            for idx, (layer_name, values) in enumerate(activations.items()):
                # Handle 2D layers (4D tensor: batch, channel, H, W)
                if idx < 4 :
                    # first 4 layers are 2d layers
                    tensor_shape = tuple(values.squeeze(0).mean(dim=0).shape) # getting shape of single feature map
                    logits = values.squeeze(0).mean(dim=0)   # taking the mean of all feature maps
                else:
                    tensor_shape = tuple(values.squeeze().shape)
                    logits = values.squeeze()   # Ensure list!
                key = f"{idx+1}"
                data[key] = {
                    "name": layer_name,
                    "shape": tensor_shape,
                    "data": logits.tolist()
                }

            # Convert input tensor to list
            data["input_tensor"] = input_tensor.squeeze().tolist()

            # Save the JSON data to a file
            with open("activations.json", "w") as f:
                json.dump(data, f, indent=4)

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
        await asyncio.Future()  # Run forever

if __name__ == "__main__":
    asyncio.run(main())
