import asyncio
import websockets

async def client():
    url= "ws://localhost:8765"
    async with websockets.connect(url) as websocket:
        print("Connected to server")
        while True:
            message = await websocket.recv()
            print(f"Received message: {message}")
asyncio.run(client())