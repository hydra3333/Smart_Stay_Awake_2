#!/usr/bin/env python3
"""
Convert an image file to base64 string for embedding in Python code
"""

import base64
from PIL import Image
import io
import os
import sys

def image_to_base64(image_path, output_format='PNG', line_length=100):
    """
    Convert image file to base64 string formatted for Python code
    
    Args:
        image_path: Path to the image file
        output_format: Format to convert to (PNG, JPEG, etc.)
        line_length: Max characters per line for code formatting
    
    Returns:
        Formatted Python code string
    """
    
    try:
        # Check if file exists
        if not os.path.exists(image_path):
            print(f"Error: File '{image_path}' not found!")
            return None
            
        # Load and process the image
        print(f"Loading image: {image_path}")
        img = Image.open(image_path)
        
        # Convert to RGBA if not already
        if img.mode != 'RGBA':
            img = img.convert('RGBA')
            print(f"Converted from {img.mode} to RGBA")
        
        # Save to memory buffer in specified format
        buffer = io.BytesIO()
        img.save(buffer, format=output_format.upper())
        img_data = buffer.getvalue()
        
        # Convert to base64
        base64_string = base64.b64encode(img_data).decode('utf-8')
        print(f"Image Path: {image_path}")
        print(f"Image size: {len(img_data)} bytes")
        print(f"Base64 size: {len(base64_string)} characters")
        
        # Split into lines for code formatting
        lines = []
        for i in range(0, len(base64_string), line_length):
            chunk = base64_string[i:i+line_length]
            ###lines.append(f'            "{chunk}"')
            lines.append(f'{chunk}')
        
        #### Format as Python code
        ###python_code = f'''        # Embedded base64 image data ({output_format}, {len(img_data)} bytes)
        ###EYE_IMAGE_BASE64 = (            {chr(10).join(lines)}
        ###)'''
        ###return python_code

        return '\n'.join(lines) + '\n'
        
    except Exception as e:
        print(f"Error processing image: {e}")
        return None

def main():
    """Main function"""
    print("Image to Base64 Converter for Python")
    print("=" * 40)
    
    # Get image file path
    if len(sys.argv) > 1:
        image_path = sys.argv[1]
    else:
        image_path = input("Enter path to your Eye of Horus image: ").strip().strip('"\'')
    
    # Convert image
    result = image_to_base64(image_path, 'PNG', line_length=120)
    
    if result:
        print("\n" + "=" * 50)
        print("SUCCESS! Copy this code into your Python file:")
        print("=" * 50)
        print(result)
        print("=" * 50)
        
        # Save to file
        output_file = "base64_image_data.txt"
        with open(output_file, 'w') as f:
            f.write(result)
        print(f"\nAlso saved to: {output_file}")
        
        # Instructions
        print(f"\nInstructions:")
        print(f"1. Copy the IMAGE_DATA section above")
        print(f"2. Replace the placeholder in your Stay_Awake.py file")
        print(f"3. The image will be embedded in your executable")
        
    else:
        print("Failed to convert image. Please check the file path and try again.")

if __name__ == "__main__":
    main()