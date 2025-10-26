# server_realtime.py
import asyncio
import json
import torch
import torch.nn.functional as F
import websockets
import numpy as np

# A small example CNN forward function
def run_conv_layer(input_tensor):
    kernel = torch.randn(1, 1, 3, 3)
    out = F.conv2d(input_tensor, kernel)
    return out.squeeze().tolist()

async def handler(websocket):
    print("Unity connected!")

    # Send periodic CNN updates
    while True:
        # Dummy 5x5 input
        x = torch.randn(1, 1, 5, 5)
        y = run_conv_layer(x)

        message = {
            "type": "conv_layer",
            "shape": [len(y), len(y[0])],
            "data": y
        }
        await websocket.send(json.dumps(message))
        await asyncio.sleep(1.0)  # send every second

async def main():
    async with websockets.serve(handler, "0.0.0.0", 8765):
        print("WebSocket server running on ws://localhost:8765")
        await asyncio.Future()  # run forever

if __name__ == "__main__":
    asyncio.run(main())
