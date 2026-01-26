#!/usr/bin/env python3
"""
Analyze DICOM file structure - run this on Windows with the output file
Usage: python analyze_dicom.py <path_to_dicom_file>
"""
import sys

try:
    import pydicom
except ImportError:
    print("Installing pydicom...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pydicom"])
    import pydicom

def analyze_dicom(filepath):
    print(f"\n=== Analyzing: {filepath} ===\n")

    try:
        dcm = pydicom.dcmread(filepath)
    except Exception as e:
        print(f"ERROR: Could not read DICOM file: {e}")
        return

    print("=== FILE META INFORMATION ===")
    for elem in dcm.file_meta:
        print(f"{elem.tag} {elem.keyword}: {elem.value}")

    print("\n=== KEY DICOM ELEMENTS ===")
    key_tags = [
        (0x0008, 0x0016),  # SOPClassUID
        (0x0008, 0x0060),  # Modality
        (0x0008, 0x0064),  # ConversionType
        (0x0018, 0x0040),  # CineRate
        (0x0018, 0x0072),  # EffectiveDuration
        (0x0018, 0x1063),  # FrameTime
        (0x0028, 0x0002),  # SamplesPerPixel
        (0x0028, 0x0004),  # PhotometricInterpretation
        (0x0028, 0x0008),  # NumberOfFrames
        (0x0028, 0x0010),  # Rows
        (0x0028, 0x0011),  # Columns
        (0x0028, 0x2110),  # LossyImageCompression
    ]

    for tag in key_tags:
        try:
            elem = dcm[tag]
            print(f"{elem.tag} {elem.keyword} ({elem.VR}): {repr(elem.value)}")
        except KeyError:
            print(f"{tag} - NOT FOUND")

    print("\n=== PIXEL DATA ANALYSIS ===")
    if (0x7fe0, 0x0010) in dcm:
        pd = dcm[(0x7fe0, 0x0010)]
        print(f"PixelData VR: {pd.VR}")
        print(f"Is undefined length: {pd.is_undefined_length}")

        raw = pd.value
        print(f"Total pixel data size: {len(raw)} bytes")
        print(f"First 50 bytes (hex): {raw[:50].hex()}")

        # Check for MP4 signature
        if b'ftyp' in raw[:100]:
            ftyp_pos = raw.find(b'ftyp')
            print(f"\nMP4 'ftyp' found at position: {ftyp_pos}")
            # Extract brand
            brand_start = ftyp_pos + 4
            brand = raw[brand_start:brand_start+4]
            print(f"MP4 brand: {brand}")
        else:
            print("\nWARNING: No MP4 'ftyp' signature found in first 100 bytes!")

        # Check encapsulation structure
        print("\n=== ENCAPSULATION STRUCTURE ===")
        pos = 0
        item_count = 0
        while pos < min(len(raw), 200):
            if raw[pos:pos+4] == bytes([0xfe, 0xff, 0x00, 0xe0]):  # Item tag
                length = int.from_bytes(raw[pos+4:pos+8], 'little')
                print(f"Item {item_count} at offset {pos}: length={length}")
                item_count += 1
                if length == 0:
                    pos += 8
                elif length == 0xffffffff:
                    pos += 8
                else:
                    pos += 8 + length
                    break  # Stop after first data item for brevity
            elif raw[pos:pos+4] == bytes([0xfe, 0xff, 0xdd, 0xe0]):  # Sequence delimiter
                print(f"Sequence delimiter at offset {pos}")
                break
            else:
                pos += 1
    else:
        print("ERROR: No PixelData found!")

    # Try to extract and check the video
    print("\n=== VIDEO STREAM CHECK ===")
    try:
        raw = dcm[(0x7fe0, 0x0010)].value
        # Find start of actual video data (after DICOM encapsulation items)
        # Look for ftyp or mdat
        video_start = None
        for i in range(min(len(raw), 200)):
            if raw[i:i+4] == b'ftyp' or raw[i:i+4] == b'mdat':
                # Go back 4 bytes for the size field
                video_start = i - 4
                break

        if video_start and video_start > 0:
            # Check the atom structure
            size = int.from_bytes(raw[video_start:video_start+4], 'big')
            atom_type = raw[video_start+4:video_start+8]
            print(f"First MP4 atom: size={size}, type={atom_type}")

            # Check H.264 profile if we can find avcC box
            avc_pos = raw.find(b'avcC')
            if avc_pos > 0:
                # avcC structure: version(1) + profile(1) + compat(1) + level(1)
                profile_idc = raw[avc_pos + 5]
                level_idc = raw[avc_pos + 7]
                profile_names = {66: 'Baseline', 77: 'Main', 100: 'High'}
                print(f"H.264 Profile: {profile_names.get(profile_idc, 'Unknown')} ({profile_idc})")
                print(f"H.264 Level: {level_idc / 10:.1f}")
            else:
                print("Could not find avcC box to determine H.264 profile")
    except Exception as e:
        print(f"Error checking video stream: {e}")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python analyze_dicom.py <path_to_dicom_file>")
        sys.exit(1)

    analyze_dicom(sys.argv[1])
