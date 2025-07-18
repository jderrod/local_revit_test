import numpy as np
from stl import mesh
from mpl_toolkits import mplot3d
import matplotlib.pyplot as plt
import os

# --- Configuration ---
stl_file_path = os.path.join(
    'RevitFamilyToStl', 'bin', 'x64', 'Debug', 'output', 'D1.stl'
)

# --- Main Script ---
if not os.path.exists(stl_file_path):
    print(f"Error: STL file not found at {os.path.abspath(stl_file_path)}")
else:
    # Create a new plot
    fig = plt.figure(figsize=(10, 8))
    ax = fig.add_subplot(111, projection='3d')

    # Load the STL files and add the vectors to the plot
    your_mesh = mesh.Mesh.from_file(stl_file_path)
    
    # Create a collection of polygons
    polygons = mplot3d.art3d.Poly3DCollection(your_mesh.vectors)
    polygons.set_edgecolor('black') # Add edges for better visibility
    ax.add_collection3d(polygons)

    # Auto scale to the mesh size
    scale = your_mesh.points.flatten()
    ax.auto_scale_xyz(scale, scale, scale)

    # Set labels and title
    ax.set_xlabel('X-axis')
    ax.set_ylabel('Y-axis')
    ax.set_zlabel('Z-axis')
    ax.set_title('STL Model Viewer')

    # Show the plot
    plt.show()
