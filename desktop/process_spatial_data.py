import os
import glob
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import tifffile
import laspy
from scipy.ndimage import gaussian_filter, distance_transform_edt

DATA_DIR = r"D:\1. DOKUMEN\Data ecw laz"
ASSETS_DIR = r"d:\4. PROJECT\20. Astha\Virexa v.2\Assets"

# Unified Extent in UTM Zone 50N coordinates
# Covering all LAZ data and the GeoTIFF
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
    
    # GeoTIFF metadata bounds
    # Tiepoint: (570144.23, 115199.11), pixel scale ~0.5m
    tif_x0 = 570144.2313292557
    tif_y0 = 115199.11244981996
    # Note: page 1 is downsampled by 2
    scale_factor = 2.0
    tif_dx = 0.49983063446086445 * scale_factor
    tif_dy = 0.4998306344608642 * scale_factor
    
    tif_w = idx_img.shape[1] * tif_dx
    tif_h = idx_img.shape[0] * tif_dy
    tif_x1 = tif_x0 + tif_w
    tif_y1 = tif_y0 - tif_h
    
    print(f"GeoTIFF Extent: X [{tif_x0:.2f}, {tif_x1:.2f}], Y [{tif_y1:.2f}, {tif_y0:.2f}]")
    
    # Render onto Unified Canvas (4096 x 3413 or 4096 x 4096)
    CANVAS_W = 4096
    CANVAS_H = int(4096 * (HEIGHT / WIDTH)) # ~3413
    canvas = Image.new("RGB", (CANVAS_W, CANVAS_H), (45, 52, 54)) # Mining dark slate backdrop
    
    # Convert RGB array to PIL Image
    pil_ortho = Image.fromarray(rgb)
    
    # Map GeoTIFF bounding box to Canvas coordinates
    # Unity/Image pixel space: (0,0) is top-left (ORIGIN_X, MAX_Y)
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

# ==========================================
# 2. PROCESS LIDAR LAZ ELEVATION (DEM)
# ==========================================
print("\n--- Processing LiDAR LAZ Elevation Points ---")
# Grid resolution for Unity Terrain / Mesh (e.g. 1024 x 1024 or 1200 x 1000)
GRID_W = 600   # 10m per cell for high-speed lightweight loading
GRID_H = 500   # 10m per cell
cell_size_x = WIDTH / GRID_W   # 10.0 m
cell_size_y = HEIGHT / GRID_H  # 10.0 m

dem_sum = np.zeros((GRID_H, GRID_W), dtype=np.float64)
dem_count = np.zeros((GRID_H, GRID_W), dtype=np.int32)
dem_min = np.full((GRID_H, GRID_W), 99999.0, dtype=np.float32)

laz_files = sorted(glob.glob(os.path.join(DATA_DIR, "*.laz")))
for lf_path in laz_files:
    print(f"Reading {os.path.basename(lf_path)}...")
    with laspy.open(lf_path) as lf:
        # Read in chunks to be memory-efficient
        for points in lf.chunk_iterator(1000000):
            x = np.asarray(points.x)
            y = np.asarray(points.y)
            z = np.asarray(points.z)
            
            # Filter within bounds
            valid = (x >= ORIGIN_X) & (x < MAX_X) & (y >= ORIGIN_Y) & (y < MAX_Y)
            x_v = x[valid]
            y_v = y[valid]
            z_v = z[valid]
            
            col = ((x_v - ORIGIN_X) / cell_size_x).astype(np.int32)
            # In grid, row 0 is top (MAX_Y), row GRID_H-1 is bottom (ORIGIN_Y)
            row = ((MAX_Y - y_v) / cell_size_y).astype(np.int32)
            
            col = np.clip(col, 0, GRID_W - 1)
            row = np.clip(row, 0, GRID_H - 1)
            
            np.add.at(dem_sum, (row, col), z_v)
            np.add.at(dem_count, (row, col), 1)

# Compute mean elevation where points exist
mask_valid = dem_count > 0
dem_grid = np.zeros((GRID_H, GRID_W), dtype=np.float32)
dem_grid[mask_valid] = (dem_sum[mask_valid] / dem_count[mask_valid]).astype(np.float32)

print(f"Valid DEM cells: {np.sum(mask_valid)} / {GRID_W * GRID_H} ({np.sum(mask_valid)/(GRID_W*GRID_H)*100:.1f}%)")
min_elev = float(dem_grid[mask_valid].min())
max_elev = float(dem_grid[mask_valid].max())
print(f"LiDAR Elevation Range: {min_elev:.2f} m to {max_elev:.2f} m (Span: {max_elev - min_elev:.2f} m)")

# Fill unmeasured / outer cells smoothly using distance transform & gaussian inpainting
# Baseline natural ground level ~ 150m
baseline_elev = 150.0
dem_filled = np.copy(dem_grid)

# Step 1: Nearest neighbor fill
indices = distance_transform_edt(~mask_valid, return_distances=False, return_indices=True)
dem_nearest = dem_grid[tuple(indices)]
dem_filled[~mask_valid] = dem_nearest[~mask_valid]

# Step 2: Smooth transition
dem_smoothed = gaussian_filter(dem_filled, sigma=1.5)
# Keep scanned pit areas high-detail, blend outer edges
dem_final = np.where(mask_valid, dem_grid, dem_smoothed)
dem_final = gaussian_filter(dem_final, sigma=0.5)

# Save Heightmap as 16-bit PNG and RAW for Unity
norm_height = (dem_final - min_elev) / (max_elev - min_elev)
norm_height_16 = (norm_height * 65535.0).astype(np.uint16)

heightmap_img = Image.fromarray(norm_height_16)
heightmap_path = os.path.join(ASSETS_DIR, "Textures", "terrain_heightmap_16bit.png")
heightmap_img.save(heightmap_path)
print(f"Saved: {heightmap_path}")

# ==========================================
# 3. GENERATE 3D TERRAIN MESH (.OBJ)
# ==========================================
print("\n--- Generating 3D Terrain Mesh (OBJ) ---")
# Build a high-performance grid mesh (e.g. 300 x 250 vertices for ~150k tris - fast 60+ FPS in Unity)
MESH_W = 300
MESH_H = 250
step_x = WIDTH / (MESH_W - 1)
step_y = HEIGHT / (MESH_H - 1)

# Sample heights at mesh vertices
mesh_x_coords = np.linspace(0, GRID_W - 1, MESH_W)
mesh_y_coords = np.linspace(0, GRID_H - 1, MESH_H)
gx, gy = np.meshgrid(mesh_x_coords, mesh_y_coords)

from scipy.ndimage import map_coordinates
mesh_elevs = map_coordinates(dem_final, [gy, gx], order=1)

obj_path = os.path.join(ASSETS_DIR, "Data", "terrain_mine_mesh.obj")
with open(obj_path, "w") as obj_file:
    obj_file.write("# FMS Mine Site 3D Terrain Mesh\n")
    obj_file.write(f"# Extent: {WIDTH}m x {HEIGHT}m, Elevation: {min_elev:.1f}m - {max_elev:.1f}m\n")
    obj_file.write("mtllib terrain_mine_mesh.mtl\n")
    obj_file.write("o MineTerrain\n")
    
    # Vertices: Unity coordinate system: X = East, Y = Up (Elevation), Z = North
    # row 0 is top (MAX_Y), row MESH_H-1 is bottom (ORIGIN_Y)
    for r in range(MESH_H):
        for c in range(MESH_W):
            ux = c * step_x
            uz = (MESH_H - 1 - r) * step_y # North
            uy = float(mesh_elevs[r, c])
            obj_file.write(f"v {ux:.2f} {uy:.2f} {uz:.2f}\n")
            
    # UV Texture coordinates (0 to 1)
    for r in range(MESH_H):
        for c in range(MESH_W):
            u = c / (MESH_W - 1)
            v = 1.0 - (r / (MESH_H - 1))
            obj_file.write(f"vt {u:.5f} {v:.5f}\n")
            
    # Normals placeholder
    obj_file.write("vn 0.0 1.0 0.0\n")
    obj_file.write("usemtl TerrainMaterial\n")
    obj_file.write("s 1\n")
    
    # Faces (Triangles)
    for r in range(MESH_H - 1):
        for c in range(MESH_W - 1):
            # 1-based indexing
            # top-left, top-right, bottom-left, bottom-right
            tl = r * MESH_W + c + 1
            tr = r * MESH_W + (c + 1) + 1
            bl = (r + 1) * MESH_W + c + 1
            br = (r + 1) * MESH_W + (c + 1) + 1
            
            # Triangle 1: tl -> bl -> tr
            obj_file.write(f"f {tl}/{tl}/1 {bl}/{bl}/1 {tr}/{tr}/1\n")
            # Triangle 2: tr -> bl -> br
            obj_file.write(f"f {tr}/{tr}/1 {bl}/{bl}/1 {br}/{br}/1\n")

print(f"Saved: {obj_path} ({MESH_W * MESH_H} vertices, {(MESH_W-1)*(MESH_H-1)*2} tris)")

# ==========================================
# 4. GENERATE CONTOUR OVERLAY TEXTURE
# ==========================================
print("\n--- Generating Contour Lines Texture ---")
contour_img = Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
draw = ImageDraw.Draw(contour_img)

# Upsample dem_final to canvas size for smooth contour tracing
import matplotlib.pyplot as plt

plt.figure(figsize=(12, 10), dpi=150)
ax = plt.subplot(1, 1, 1)
ax.set_position([0, 0, 1, 1])
ax.set_axis_off()

# Contour intervals: 5m minor, 25m major (Index contours)
levels_minor = np.arange(int(min_elev // 5) * 5, int(max_elev // 5 + 1) * 5, 5)
levels_major = np.arange(int(min_elev // 25) * 25, int(max_elev // 25 + 1) * 25, 25)

# Flip vertically because row 0 is top
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
print("\n--- Generating Metadata & Mine Waypoints ---")
# Haul road routes from Pit to Disposal & Sump
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
