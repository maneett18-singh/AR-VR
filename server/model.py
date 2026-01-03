from torch import nn
import torch
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
        x = x.view(x.size(0), -1)  # Flatten
        x = self.relu(self.fc1(x))
        x = self.dropout2(x)
        x = self.fc2(x)
        return x
    
    def load_weights(self, path: str, map_location= "cpu"):
        return self.load_state_dict(state_dict=torch.load(path, weights_only=True, map_location="cpu"))
    
from torchvision import transforms
transform = transforms.Compose([
    transforms.Resize((28, 28)),          # Resize any image to 28x28
    transforms.Grayscale(num_output_channels=1),  # Convert to 1 channel (grayscale)
    transforms.ToTensor(),                # Convert to tensor
    transforms.Normalize((0.1307,), (0.3081,))   # Normalize like MNIST
])

import requests
from PIL import Image
from io import BytesIO


def get_input_tensor(input_url="https://conx.readthedocs.io/en/latest/_images/MNIST_6_0.png"):
    response = requests.get(input_url)
    if response.status_code == 200:
        # Step 2: Open the image with PIL
        image = Image.open(BytesIO(response.content))
        img_bytes = response.content  # raw bytes directly from requests
    else:
        print(f"Failed to download image. Status code: {response.status_code}")
    input_tensor = transform(image)           # shape: [1, 28, 28]
    input_tensor = input_tensor.unsqueeze(0)  # add batch dimension -> shape [1, 1, 28, 28]
    return input_tensor, img_bytes
    

def make_prediction(model,input_url="https://conx.readthedocs.io/en/latest/_images/MNIST_6_0.png"):
    input_tensor, _ = get_input_tensor(input_url)
    if input_tensor is None:
        return  # Failed to get input tensor
    model.eval()
    with torch.no_grad():
        output = model(input_tensor)
        predicted = torch.argmax(output, dim=1)
        print("Predicted class:", predicted.item())
        





def get_activations(model):
    activations = {}
    hook_handles = {}

    def get_hook(name):
        def hook(module, input, output):
            activations[name] = output
        return hook

    for idx, layer in enumerate(model.children()):
        layer_name = f"layer_{idx+1}"  # layer_1, layer_2, ...
        hook_handles[layer_name] = layer.register_forward_hook(get_hook(layer_name))

    

    return activations


import matplotlib.pyplot as plt

def plot_activations(input_tensor):
    x = input_tensor.squeeze(0)      # remove batch dim
    act_2d = x.mean(dim=0)           # collapse channels -> [H, W]
    print("shape of the input:", act_2d.shape)
    
    plt.figure(figsize=(6, 6))
    plt.imshow(act_2d.cpu().numpy(), cmap='hot')  # 'hot' or 'viridis' for heatmap
    plt.colorbar()                                # show color scale
    plt.axis('off')
    plt.show()



    

