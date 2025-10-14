import base64

# Test decoding one of the base64 strings from the map
test_data = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAAAdgAAAHYBTnsmCAAAABl0RVh0U29mdHdhcmUAd3d3Lmlua3NjYXBlLm9yZ5vuPBoAAABPSURBVFiF7dOxDQAgDATBv0r6L5kOqIFK7AUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP+xkjRJ6u4LAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB8ZQNpvAXVo8g0ZQAAAABJRU5ErkJggg=="

try:
    decoded = base64.b64decode(test_data)
    print(f"Decoded successfully: {len(decoded)} bytes")
    # Check PNG signature
    if decoded[:8] == b'\x89PNG\r\n\x1a\n':
        print("Valid PNG signature")
    else:
        print("Invalid PNG signature!")
        print(f"First 8 bytes: {decoded[:8]}")
except Exception as e:
    print(f"Error: {e}")
