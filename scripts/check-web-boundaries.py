"""Compatibility entry point for the native architecture check."""
import runpy
from pathlib import Path
runpy.run_path(str(Path(__file__).with_name("check-native-boundaries.py")), run_name="__main__")
