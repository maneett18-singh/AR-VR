# cnn_to_json.py
import math
import torch
from torch import nn
from torchvision import transforms
import requests
from PIL import Image
from io import BytesIO
import matplotlib.pyplot as plt
import base64
import json

# ---------------------------
# CNN model
# ---------------------------
class model(nn.Module):
    def __init__(self):
        super(model, self).__init__()
        self.conv1 = nn.Conv2d(1, 32, kernel_size=3, padding=1)
        self.conv2 = nn.Conv2d(32, 64, kernel_size=3, padding=1)
        self.pool = nn.MaxPool2d(2, 2)
        self.dropout1 = nn.Dropout(0.25)
        self.fc1 = nn.Linear(64*14*14, 128)
        self.dropout2 = nn.Dropout(0.5)
        self.fc2 = nn.Linear(128, 10)
        self.relu = nn.ReLU()

    def forward(self, x):
        x = self.relu(self.conv1(x))
        x = self.relu(self.conv2(x))
        x = self.pool(x)
        x = self.dropout1(x)
        x = x.view(x.size(0), -1)
        x = self.relu(self.fc1(x))
        x = self.dropout2(x)
        x = self.fc2(x)
        return x

    def load_weights(self, path: str, map_location="cpu"):
        return self.load_state_dict(torch.load(path, map_location=map_location, weights_only=True))

# ---------------------------
# Input and transforms
# ---------------------------
transform = transforms.Compose([
    transforms.Resize((28, 28)),
    transforms.Grayscale(num_output_channels=1),
    transforms.ToTensor(),
    transforms.Normalize((0.1307,), (0.3081,))
])

def get_input_tensor(input_url="https://conx.readthedocs.io/en/latest/_images/MNIST_6_0.png"):
    response = requests.get(input_url)
    if response.status_code != 200:
        raise ValueError(f"Failed to download image. Status code: {response.status_code}")
    image = Image.open(BytesIO(response.content))
    tensor = torch.tensor(transform(image), dtype=torch.float32)
    input_tensor = torch.unsqueeze(tensor, 0)
    return input_tensor, image

# ---------------------------
# Activations hooks
# ---------------------------
def get_all_activations(model):
    """
    Register forward hooks for all layers in the model.
    Returns:
        activations: dict mapping layer_name -> output tensor
        hooks: list of hook handles (needed to remove hooks later)
    """
    activations = {}
    hooks = []

    def hook_fn(name):
        def hook(module, input, output):
            activations[name] = output
        return hook

    # Iterate over all named children
    for name, module in model.named_children():
        # You can choose to skip certain layers if needed
        # e.g., skip Dropout layers if you don't care
        hooks.append(module.register_forward_hook(hook_fn(name)))
        print(f"Registered hook for layer: {name}")

    return activations, hooks

# ---------------------------
# General activations hooks for all layers
# ---------------------------

def get_activations(model):
    activations = {}

    def get_hook(name):
        def hook(module, input, output):
            activations[name] = output
        return hook

    # Register hooks with meaningful layer names
    activations_handles = [
        model.conv1.register_forward_hook(get_hook("conv1")),
        model.conv2.register_forward_hook(get_hook("conv2")),
        model.pool.register_forward_hook(get_hook("pool"))
    ]

    return activations


# ---------------------------
# Make prediction
# ---------------------------

def make_prediction(model, input_tensor):
    model.eval()
    with torch.no_grad():
        output = model(input_tensor)
        predicted = torch.argmax(output, dim=1)
        print("Predicted class:", predicted.item())
        return predicted.item()


# ---------------------------
# Utility to convert tensor to base64 image
# ---------------------------
def tensor_to_base64(tensor):
    """
    Convert [H, W] or [C,H,W] tensor to a base64 PNG image
    """
    import io
    import numpy as np
    from PIL import Image
    import base64

    tensor = tensor.detach().cpu()
    
    # Remove batch dimension if present
    if tensor.ndim == 4 and tensor.shape[0] == 1:
        tensor = tensor[0]  # [C, H, W]
    
    # Collapse channel dimension if exists
    if tensor.ndim == 3:
        tensor = tensor.mean(dim=0)  # [H, W]
    
    # Normalize 0-1 and convert to uint8
    tensor = (tensor - tensor.min()) / (tensor.max() - tensor.min() + 1e-8)  # avoid div by zero
    array = (tensor.numpy() * 255).astype('uint8')
    
    img = Image.fromarray(array)
    buffered = io.BytesIO()
    img.save(buffered, format="PNG")
    return base64.b64encode(buffered.getvalue()).decode("utf-8")


# ---------------------------
# CNN summary as dict
# ---------------------------
def get_cnn_summary(model, input_tensor):
    summary = []
    x = input_tensor
    for name, layer in model.named_children():
        input_shape = tuple(x.shape)
        kernel, stride, padding = "-", "-", "-"
        if isinstance(layer, nn.Conv2d):
            kernel, stride, padding = layer.kernel_size, layer.stride, layer.padding
            x = layer(x)
        elif isinstance(layer, nn.MaxPool2d):
            kernel, stride, padding = layer.kernel_size, layer.stride, layer.padding
            x = layer(x)
        elif isinstance(layer, nn.Linear):
            x = x.view(x.size(0), -1)
            x = layer(x)
        else:
            x = layer(x)
        output_shape = tuple(x.shape)
        channels = x.shape[1] if len(x.shape) > 1 else "-"
        summary.append({
            "layer": name,
            "type": layer.__class__.__name__,
            "kernel": str(kernel),
            "stride": str(stride),
            "padding": str(padding),
            "input_shape": input_shape,
            "output_shape": output_shape,
            "channels": channels
        })
    return summary


def show_feature_maps(fmaps, n_cols=8):
    # If fmaps is a dict of dicts, convert to list for plotting
    if isinstance(fmaps, dict):
        fmaps_list = []
        for idx, fmap in fmaps.items():
            fmap_dict = {"map_index": idx, **fmap}
            fmaps_list.append(fmap_dict)
        fmaps = fmaps_list

    n_maps = len(fmaps)
    n_rows = (n_maps + n_cols - 1) // n_cols
    fig, axes = plt.subplots(n_rows, n_cols, figsize=(n_cols*2, n_rows*2))
    axes = axes.flatten()

    for i, fmap_dict in enumerate(fmaps):
        img_data = base64.b64decode(fmap_dict["base64"])
        img = Image.open(BytesIO(img_data))
        axes[i].imshow(img, cmap="viridis")
        axes[i].axis("off")
        axes[i].set_title(f"Map {fmap_dict['map_index']}\n{fmap_dict['shape']}", fontsize=8)

    for j in range(len(fmaps), len(axes)):
        axes[j].axis("off")

    plt.tight_layout()
    plt.show()



# ---------------------------
# Main workflow
# ---------------------------
if __name__ == "__main__":
    cnn_model = model()
    cnn_model.load_weights("mnist_cnn.pth")

    input_tensor, input_image = get_input_tensor()
    activations, hooks = get_all_activations(cnn_model)

    cnn_model.eval()
    with torch.no_grad():
        output = cnn_model(input_tensor)
        predicted_class = torch.argmax(output, dim=1).item()

    # Convert input image to base64
    import io
    buffered = io.BytesIO()
    input_image.save(buffered, format="PNG")
    input_base64 = base64.b64encode(buffered.getvalue()).decode("utf-8")

    # Convert feature maps to base64
    feature_maps_base64 = {}
    for name, tensor in activations.items():
        maps = []
        for i in range(tensor.shape[1] if tensor.ndim==4 else tensor.shape[0]):
            maps.append(tensor_to_base64(tensor[0,i] if tensor.ndim==4 else tensor[i]))
        feature_maps_base64[name] = maps

    # CNN summary
    cnn_summary = get_cnn_summary(cnn_model, input_tensor)

    # Dump all to JSON
    # output_json = {
    #     "input_image_base64": input_base64,
    #     "predicted_class": predicted_class,
    #     "cnn_summary": cnn_summary,
    #     "feature_maps": feature_maps_base64
    # }

    # with open("cnn_output.json", "w") as f:
    #     json.dump(output_json, f, indent=4)

    # print("Saved CNN output with feature maps to cnn_output.json")
