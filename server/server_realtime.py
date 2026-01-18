import asyncio
import json
import torch
import torch.nn.functional as F
import websockets
import numpy as np
import base64
from io import BytesIO
from PIL import Image
from model import *

async def handler(websocket):
    print("✅ Unity connected!")

    # Load model once
    cnn = model()
    try:
        cnn.load_weights(path="mnist_cnn.pth")
        print("✅ Model weights loaded")
    except Exception as e:
        print(f"⚠️ Could not load weights: {e}")
    
    cnn.eval()
    
    while True:
        try:
            # Get input image tensor
            image_url = "https://machinelearningmastery.com/wp-content/uploads/2019/02/sample_image-300x298.png"
            input_tensor, image_bytes = get_input_tensor(image_url)
            print(f"📨 Got input tensor shape: {input_tensor.shape}")
            
            # 🎯 Register hooks to capture activations
            activations = {}
            hook_handles = {}
            
            def get_hook(name):
                def hook(module, input, output):
                    activations[name] = output.detach()
                return hook
            
            # Register hooks on all layers
            for name, module in cnn.named_modules():
                if isinstance(module, (torch.nn.Conv2d, torch.nn.Linear)):
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

            # 🎯 Convert input image to base64
            input_image_base64 = base64.b64encode(image_bytes).decode('utf-8')

            # 🎯 Build feature_maps in the correct format
            feature_maps = {}
            for layer_idx, (layer_name, activation) in enumerate(activations.items()):
                try:
                    # Get activation and normalize
                    act = activation.squeeze(0)  # Remove batch dimension
                    
                    # Handle different tensor shapes
                    if len(act.shape) == 3:  # Conv output: [C, H, W]
                        # Average across channels to get single feature map
                        act_2d = act.mean(dim=0)
                    elif len(act.shape) == 1:  # FC output
                        # Reshape to square-ish
                        size = int(np.sqrt(act.shape[0]))
                        act_2d = act[:size*size].view(size, size)
                    else:
                        continue
                    
                    # Normalize to 0-255
                    act_np = act_2d.cpu().numpy()
                    act_min, act_max = act_np.min(), act_np.max()
                    if act_max > act_min:
                        act_normalized = ((act_np - act_min) / (act_max - act_min) * 255).astype(np.uint8)
                    else:
                        act_normalized = (act_np * 255).astype(np.uint8)
                    
                    # Create image from activation
                    activation_img = Image.fromarray(act_normalized)
                    
                    # Convert to PNG base64
                    img_buffer = BytesIO()
                    activation_img.save(img_buffer, format='PNG')
                    activation_base64 = base64.b64encode(img_buffer.getvalue()).decode('utf-8')
                    
                    # Extract layer type from name (e.g., "conv1" from "conv1.weight")
                    layer_key = layer_name.split('.')[0] if '.' in layer_name else layer_name
                    
                    # Store in nested structure
                    if layer_key not in feature_maps:
                        feature_maps[layer_key] = {}
                    
                    filter_name = f"filter_{len(feature_maps[layer_key])}"
                    feature_maps[layer_key][filter_name] = {
                        "shape": list(act_2d.shape),
                        "base64": activation_base64
                    }
                    
                except Exception as e:
                    print(f"⚠️ Error processing layer {layer_name}: {e}")
                    continue

            # 🎯 Build final JSON with correct structure
            final_data = {
                "input_image_base64": input_image_base64,
                "input_image_shape": [28, 28],
                "feature_maps": feature_maps
            }

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

            print(f"✅ Sending {len(feature_maps)} feature map layers")
            await websocket.send(json.dumps(final_data))
            await asyncio.sleep(1.0)

        except websockets.exceptions.ConnectionClosed:
            print("🔌 Unity disconnected.")
            break
        except Exception as e:
            print(f"❌ Error in handler: {e}")
            import traceback
            traceback.print_exc()
            break

async def main():
    async with websockets.serve(handler, "localhost", 8765):
        print("🌐 WebSocket server running on ws://localhost:8765")
        print("⏳ Waiting for Unity connection...")
        await asyncio.Future()  # Run forever

if __name__ == "__main__":
    asyncio.run(main())
