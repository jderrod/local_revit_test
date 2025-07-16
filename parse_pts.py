import csv

def parse_pts_file(file_path):
    """
    Parses a .pts file and categorizes components into stiles, doors, and panels.

    Args:
        file_path (str): The full path to the .pts file.

    Returns:
        tuple: A tuple containing three lists (stiles, doors, panels).
    """
    stiles = []
    doors = []
    panels = []

    # Component type is expected in the 33rd column (index 32)
    COMPONENT_TYPE_INDEX = 32

    try:
        with open(file_path, 'r', newline='', encoding='utf-8') as ptsfile:
            reader = csv.reader(ptsfile)
            for row in reader:
                if len(row) > COMPONENT_TYPE_INDEX:
                    component_type = row[COMPONENT_TYPE_INDEX].strip().lower()
                    if component_type == 'stile':
                        stiles.append(row)
                    elif component_type == 'door':
                        doors.append(row)
                    elif component_type == 'panel':
                        panels.append(row)
    except FileNotFoundError:
        print(f"Error: The file was not found at {file_path}")
        return [], [], []
    except Exception as e:
        print(f"An error occurred: {e}")
        return [], [], []

    return stiles, doors, panels

import json

if __name__ == "__main__":
    pts_file = 'IBUS170157.PTS'
    stiles_list, doors_list, panels_list = parse_pts_file(pts_file)

    output_data = {
        'stiles': stiles_list,
        'doors': doors_list,
        'panels': panels_list
    }

    output_filename = 'components.json'
    with open(output_filename, 'w', encoding='utf-8') as f:
        json.dump(output_data, f, indent=4)

    print(f"Successfully parsed .pts file and saved data to {output_filename}")
    print(f"Found {len(stiles_list)} stiles, {len(doors_list)} doors, and {len(panels_list)} panels.")
