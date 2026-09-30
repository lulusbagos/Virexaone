import os
import glob
import json
import numpy as np
from PIL import Image, ImageDraw
import tifffile
import laspy
from scipy.ndimage import gaussian_filter, distance_transform_edt, map_coordinates
import matplotlib.pyplot as plt

DATA_DIR = r"D:\1. DOKUMEN\Data ecw laz"
ASSETS_DIR = r"d:\4. PROJECT\20. Astha\Virexaone\desktop\Assets"
RESOURCES_DIR = os.path.join(ASSETS_DIR, "Resources")

# Ensure target directories exist
os.makedirs(os.path.join(ASSETS_DIR, "Textures"), exist_ok=True)
os.makedirs(os.path.join(ASSETS_DIR, "Data"), exist_ok=True)
os.makedirs(os.path.join(RESOURCES_DIR, "Textures"), exist_ok=True)
os.makedirs(os.path.join(RESOURCES_DIR, "Data"), exist_ok=True)

# Unified Extent in UTM Zone 50N coordinates
ORIGIN_X = 570000.0  # Easting Min
ORIGIN_Y = 110500.0  # Northing Min
MAX_X = 576000.0     # Easting Max
MAX_Y = 115500.0     # Northing Max

WIDTH = MAX_X - ORIGIN_X    # 6000 meters
HEIGHT = MAX_Y - ORIGIN_Y   # 5000 meters

print(f"Unified Bounding Box: [{ORIGIN_X}, {MAX_X}] x [{ORIGIN_Y}, {MAX_Y}] ({WIDTH}m x {HEIGHT}m)")

# ==========================================
# 1. PROCESS GEOTIFF ORTHOPHOTO
# ==========================================
print("\n--- Processing GeoTIFF Orthophoto ---")
geotiff_path = os.path.join(DATA_DIR, "Pit Unggul_28012025.tif")
with tifffile.TiffFile(geotiff_path) as tif:
    # Use page 1 or 0 for texture
    p = tif.pages[1]  # shape ~4547 x 5539
    print(f"Reading page 1: shape {p.shape}...")
    arr = p.asarray()
    cm = np.array(p.tags['ColorMap'].value).reshape(3, 256)
    r_lut = (cm[0] / 256).astype(np.uint8)
    g_lut = (cm[1] / 256).astype(np.uint8)
    b_lut = (cm[2] / 256).astype(np.uint8)
    
    idx_img = arr[:, :, 0]
    alpha_img = arr[:, :, 1]
    
    rgb = np.zeros((idx_img.shape[0], idx_img.shape[1], 3), dtype=np.uint8)
    rgb[:, :, 0] = r_lut[idx_img]
    rgb[:, :, 1] = g_lut[idx_img]
    rgb[:, :, 2] = b_lut[idx_img]
    
    tif_x0 = 570144.2313292557
    tif_y0 = 115199.11244981996
    scale_factor = 2.0
    tif_dx = 0.49983063446086445 * scale_factor
    tif_dy = 0.4998306344608642 * scale_factor
    
    tif_w = idx_img.shape[1] * tif_dx
    tif_h = idx_img.shape[0] * tif_dy
    tif_x1 = tif_x0 + tif_w
    tif_y1 = tif_y0 - tif_h
    
    print(f"GeoTIFF Extent: X [{tif_x0:.2f}, {tif_x1:.2f}], Y [{tif_y1:.2f}, {tif_y0:.2f}]")
    
    CANVAS_W = 4096
    CANVAS_H = int(4096 * (HEIGHT / WIDTH)) # ~3413
    canvas = Image.new("RGB", (CANVAS_W, CANVAS_H), (45, 52, 54))
    
    pil_ortho = Image.fromarray(rgb)
    
    u_left = int(((tif_x0 - ORIGIN_X) / WIDTH) * CANVAS_W)
    u_top = int(((MAX_Y - tif_y0) / HEIGHT) * CANVAS_H)
    u_right = int(((tif_x1 - ORIGIN_X) / WIDTH) * CANVAS_W)
    u_bottom = int(((MAX_Y - tif_y1) / HEIGHT) * CANVAS_H)
    
    paste_w = max(1, u_right - u_left)
    paste_h = max(1, u_bottom - u_top)
    
    pil_ortho_resized = pil_ortho.resize((paste_w, paste_h), Image.Resampling.LANCZOS)
    canvas.paste(pil_ortho_resized, (u_left, u_top))
    
    # Save standard orthophoto texture
    ortho_out_path = os.path.join(ASSETS_DIR, "Textures", "geotiff_ortho_4k.jpg")
    canvas.save(ortho_out_path, "JPEG", quality=92)
    print(f"Saved: {ortho_out_path}")
    
    # Also save as PitUnggul_TIF_Texture.png and in Resources
    png_out_path = os.path.join(ASSETS_DIR, "Textures", "PitUnggul_TIF_Texture.png")
    canvas.save(png_out_path, "PNG")
    print(f"Saved: {png_out_path}")

    res_ortho_path = os.path.join(RESOURCES_DIR, "Textures", "geotiff_ortho_4k.jpg")
    canvas.save(res_ortho_path, "JPEG", quality=92)
    print(f"Saved: {res_ortho_path}")

# ==========================================
# 2. PROCESS LIDAR LAZ ELEVATION (DEM)
# ==========================================
print("\n--- Processing LiDAR LAZ Elevation Points ---")
GRID_W = 600
GRID_H = 500
cell_size_x = WIDTH / GRID_W
cell_size_y = HEIGHT / GRID_H

dem_sum = np.zeros((GRID_H, GRID_W), dtype=np.float64)
dem_count = np.zeros((GRID_H, GRID_W), dtype=np.int32)

laz_files = sorted(glob.glob(os.path.join(DATA_DIR, "*.laz")))
for lf_path in laz_files:
    print(f"Reading {os.path.basename(lf_path)}...")
    with laspy.open(lf_path) as lf:
        for points in lf.chunk_iterator(1000000):
            x = np.asarray(points.x)
            y = np.asarray(points.y)
            z = np.asarray(points.z)
            
            valid = (x >= ORIGIN_X) & (x < MAX_X) & (y >= ORIGIN_Y) & (y < MAX_Y)
            x_v = x[valid]
            y_v = y[valid]
            z_v = z[valid]
            
            col = ((x_v - ORIGIN_X) / cell_size_x).astype(np.int32)
            row = ((MAX_Y - y_v) / cell_size_y).astype(np.int32)
            
            col = np.clip(col, 0, GRID_W - 1)
            row = np.clip(row, 0, GRID_H - 1)
            
            np.add.at(dem_sum, (row, col), z_v)
            np.add.at(dem_count, (row, col), 1)

mask_valid = dem_count > 0
dem_grid = np.zeros((GRID_H, GRID_W), dtype=np.float32)
dem_grid[mask_valid] = (dem_sum[mask_valid] / dem_count[mask_valid]).astype(np.float32)

print(f"Valid DEM cells: {np.sum(mask_valid)} / {GRID_W * GRID_H} ({np.sum(mask_valid)/(GRID_W*GRID_H)*100:.1f}%)")
min_elev = float(dem_grid[mask_valid].min())
max_elev = float(dem_grid[mask_valid].max())
print(f"LiDAR Elevation Range: {min_elev:.2f} m to {max_elev:.2f} m (Span: {max_elev - min_elev:.2f} m)")

# Fill unmeasured cells
indices = distance_transform_edt(~mask_valid, return_distances=False, return_indices=True)
dem_nearest = dem_grid[tuple(indices)]
dem_filled = np.copy(dem_grid)
dem_filled[~mask_valid] = dem_nearest[~mask_valid]

dem_smoothed = gaussian_filter(dem_filled, sigma=1.5)
dem_final = np.where(mask_valid, dem_grid, dem_smoothed)
dem_final = gaussian_filter(dem_final, sigma=0.5)

# Save Heightmap as 16-bit PNG
norm_height = (dem_final - min_elev) / (max_elev - min_elev)
norm_height_16 = (norm_height * 65535.0).astype(np.uint16)
heightmap_img = Image.fromarray(norm_height_16)
heightmap_path = os.path.join(ASSETS_DIR, "Textures", "terrain_heightmap_16bit.png")
heightmap_img.save(heightmap_path)
print(f"Saved: {heightmap_path}")

# Save 513x513 RAW 16-bit little endian for Unity Terrain
TERRAIN_RES = 513
gx_raw = np.linspace(0, GRID_W - 1, TERRAIN_RES)
gy_raw = np.linspace(0, GRID_H - 1, TERRAIN_RES)
dem_raw_grid = map_coordinates(dem_final, np.meshgrid(gy_raw, gx_raw, indexing='ij'), order=1)
norm_raw = np.clip((dem_raw_grid - min_elev) / (max_elev - min_elev), 0.0, 1.0)
# In Unity Terrain, row 0 is South (bottom), row 512 is North (top), so flip vertically
norm_raw_flipped = np.flipud(norm_raw)
raw_uint16 = (norm_raw_flipped * 65535.0).astype('<u2') # little-endian uint16

raw_path = os.path.join(ASSETS_DIR, "Textures", "RealMine_Heightmap.raw")
with open(raw_path, "wb") as rf:
    rf.write(raw_uint16.tobytes())
print(f"Saved: {raw_path} ({TERRAIN_RES}x{TERRAIN_RES} uint16, {len(raw_uint16.tobytes())} bytes)")

# ==========================================
# 3. GENERATE BINARY ELEVATION DATA (.BYTES)
# ==========================================
# MineTerrainLoader expects gridRows=230, gridCols=280
TERRAIN_ROWS = 230
TERRAIN_COLS = 280

gx = np.linspace(0, GRID_W - 1, TERRAIN_COLS)
gy = np.linspace(0, GRID_H - 1, TERRAIN_ROWS)
mesh_elevs = map_coordinates(dem_final, np.meshgrid(gy, gx, indexing='ij'), order=1).astype(np.float32)

bytes_path = os.path.join(ASSETS_DIR, "Data", "terrain_elevation.bytes")
with open(bytes_path, "wb") as bf:
    bf.write(mesh_elevs.tobytes())
print(f"Saved: {bytes_path} ({TERRAIN_ROWS}x{TERRAIN_COLS} float32 array, {len(mesh_elevs.tobytes())} bytes)")

res_bytes_path = os.path.join(RESOURCES_DIR, "Data", "terrain_elevation.bytes")
with open(res_bytes_path, "wb") as bf:
    bf.write(mesh_elevs.tobytes())
print(f"Saved: {res_bytes_path}")

# ==========================================
# 4. GENERATE CONTOUR OVERLAY TEXTURE
# ==========================================
print("\n--- Generating Contour Lines Texture ---")
plt.figure(figsize=(12, 10), dpi=150)
ax = plt.subplot(1, 1, 1)
ax.set_position([0, 0, 1, 1])
ax.set_axis_off()

levels_minor = np.arange(int(min_elev // 5) * 5, int(max_elev // 5 + 1) * 5, 5)
levels_major = np.arange(int(min_elev // 25) * 25, int(max_elev // 25 + 1) * 25, 25)

ax.contour(dem_final, levels=levels_minor, colors='#ffcc00', linewidths=0.6, alpha=0.6)
cs = ax.contour(dem_final, levels=levels_major, colors='#ff5500', linewidths=1.2, alpha=0.9)
ax.clabel(cs, inline=True, fontsize=6, fmt='%d m')

contour_plot_path = os.path.join(ASSETS_DIR, "Textures", "contour_overlay.png")
plt.savefig(contour_plot_path, transparent=True, bbox_inches='tight', pad_inches=0)
plt.close()
print(f"Saved: {contour_plot_path}")

# ==========================================
# 5. GENERATE METADATA & WAYPOINTS JSON
# ==========================================
metadata = {
    "crs": "WGS 84 / UTM zone 50N",
    "origin_utm": {"x": ORIGIN_X, "y": ORIGIN_Y, "z": 0.0},
    "max_utm": {"x": MAX_X, "y": MAX_Y, "z": max_elev},
    "dimensions": {"width_m": WIDTH, "height_m": HEIGHT, "elevation_min": min_elev, "elevation_max": max_elev},
    "locations": [
        {"id": "PIT_PANEL_EAST", "name": "Pit Panel East (Digging Bench)", "utm": [574700.0, 112800.0, 115.0], "type": "LoadingPoint"},
        {"id": "PIT_T6U", "name": "Pit T6U Loading Zone", "utm": [573800.0, 114500.0, 85.0], "type": "LoadingPoint"},
        {"id": "PIT_T6S33", "name": "Pit T6S33 Deep Mining", "utm": [572950.0, 112300.0, 110.0], "type": "LoadingPoint"},
        {"id": "DISPOSAL_MAIN", "name": "Main Waste Disposal Dump", "utm": [571200.0, 112850.0, 260.0], "type": "DumpingPoint"},
        {"id": "DISPOSAL_NORTH", "name": "North Waste Dump Facility", "utm": [571800.0, 113400.0, 290.0], "type": "DumpingPoint"},
        {"id": "SUMP_CENTRAL", "name": "Central Pit Sump & Dewatering", "utm": [573600.0, 113700.0, 75.0], "type": "Sump"},
        {"id": "MINE_OFFICE", "name": "Mine Operations & Dispatch Center", "utm": [571000.0, 111500.0, 175.0], "type": "Office"},
        {"id": "WORKSHOP_MAIN", "name": "Heavy Equipment Workshop", "utm": [571500.0, 111800.0, 180.0], "type": "Workshop"},
        {"id": "FUEL_STATION", "name": "Fuel Farm & Lube Station", "utm": [571350.0, 111650.0, 178.0], "type": "Fuel"}
    ],
    "haul_routes": [
        {
            "route_id": "ROUTE_PANEL_EAST_TO_DISPOSAL",
            "name": "Panel East -> Disposal Main Haul Route",
            "waypoints_utm": [
                [574700.0, 112800.0, 115.0],
                [574300.0, 112950.0, 130.0],
                [573800.0, 113100.0, 155.0],
                [573300.0, 113000.0, 180.0],
                [572700.0, 112900.0, 210.0],
                [572000.0, 112850.0, 240.0],
                [571200.0, 112850.0, 260.0]
            ]
        },
        {
            "route_id": "ROUTE_T6U_TO_DISPOSAL_NORTH",
            "name": "Pit T6U -> North Dump Haul Route",
            "waypoints_utm": [
                [573800.0, 114500.0, 85.0],
                [573500.0, 114200.0, 120.0],
                [573100.0, 113900.0, 170.0],
                [572600.0, 113600.0, 220.0],
                [572100.0, 113500.0, 260.0],
                [571800.0, 113400.0, 290.0]
            ]
        },
        {
            "route_id": "ROUTE_T6S33_TO_DISPOSAL_MAIN",
            "name": "Pit T6S33 -> Disposal Main Haul Route",
            "waypoints_utm": [
                [572950.0, 112300.0, 110.0],
                [572800.0, 112500.0, 150.0],
                [572400.0, 112700.0, 200.0],
                [571800.0, 112800.0, 245.0],
                [571200.0, 112850.0, 260.0]
            ]
        }
    ]
}

meta_path = os.path.join(ASSETS_DIR, "Data", "mine_spatial_metadata.json")
with open(meta_path, "w") as f:
    json.dump(metadata, f, indent=2)
print(f"Saved: {meta_path}")

print("\n=== All Spatial Preprocessing Completed Successfully! ===")
