# DSG-Editor-for-Unity
Lightweight Unity tool that can create Dynamic Scene Graph (DSG) for your 2D/3D scenes in Unity. 

## 💡 Features

- **Lightweight DSG generation**  
  One-click build of a dynamic scene graph (DSG) from your Unity scene.

- **Node & Edge management**  
  Define Rooms, Places, Objects; add/remove nodes and edges (containment & traversability) interactively.

- **Natural-language scene description**  
  Auto-generate JSON-formatted scene descriptions and NL summaries for downstream tasks.

---

## 💻 Usage

> **Before you begin**  
> In your Unity scene, create an empty GameObject and attach the `DSGManager` script. This object will hold all DSG data for the scene.

### 1. Open the DSG Editor window

1. In the Unity menu bar, select **Window → DSG Editor**  
2. The **DSG Editor** window will appear

---

### 2. Interface overview
<img width="592" alt="window preview" src="https://github.com/user-attachments/assets/7c1513f9-1c2e-4774-88da-a3bc2384eb2b" />


---

### 3. Node management

#### 3.1 Node types  
You can define which scene elements map to each type:

- **Room**: A top-level area  
- **Place**: A sub-region inside a Room (e.g. hallway, workspace)  
- **Object**: Any GameObject in the scene (e.g. computer, desk)

#### 3.2 Adding a node

1. **Select Node Type** from the dropdown (Room / Place / Object).  
2. **Configure Name or Object**  
   - For **Room** and **Place**, enter a unique ID in **Node Name**.  
   - For **Object**, drag a scene GameObject into **Select Object**.  
     *(Each Object must have a placeholder cube in the scene to represent its bounds.)*  
3. **Define Range** (Room/Place only)  
   - Click **Select Range (Shift+LeftDrag)**.  
   - In the Scene view, hold **Shift + Left-drag** to draw a rectangle.  
   - Release to set **Range Center** & **Range Size**.  
4. Click **Add Node to DSG** to add the node (with its position/size or object bounds) to `DSGManager.SceneGraph`.

#### 3.3 Removing a node

- In the **Current Nodes** list, click **Remove** on the desired row.  
  *(This also deletes all associated edges.)*

---

### 4. Edge management

#### 4.1 Edge types

- **Containment** (directed):  
  - Room → Place  
  - Place → Object  
- **Traversability** (undirected):  
  - Place ↔ Place

#### 4.2 Auto-generate containment edges

- Click **Generate Containment Edges** to:  
  1. Add Place→Object edges where bounding-boxes overlap  
  2. Add Room→Place edges for each Place inside a Room

#### 4.3 Manual edge operations

##### 4.3.1 Add a new edge

- **Containment**  
  1. In **Add New Containment Edge**, choose parent and child from dropdowns  
  2. Click **Add Containment Edge**  
- **Traversability**  
  1. In **Add New Traversability Edge**, select two Places  
  2. Click **Add Traversability Edge**

##### 4.3.2 Edit an edge

1. Click **Edit** next to the edge in the list  
2. Modify endpoints or type via dropdowns  
3. Click **Done** to save

##### 4.3.3 Remove an edge

- Click **Remove** next to the edge in the list

---

### 5. Scene description & validation

#### 5.1 Generate natural-language description

- Click **Generate Natural Language Description** to:  
  - List hierarchical relations of Rooms, Places, Objects  
  - Describe traversable connections between Places  
  - Output JSON at `Assets/SceneDescription.json`

#### 5.2 Check unconnected nodes

- Click **Check Unconnected Nodes** to list any nodes with no edges in the Console

---

### 6. Source file locations

- **DSG definition**: `DSG.cs`  
- **Editor window**: `DSGEditor.cs`  
- **Scene management**: `DSGManager.cs` (includes `SceneGraph`, `Node`, `Edge` classes)  

## 📃 License

Use [Apache 2.0](./LICENSE) , see LICENSE file

## 📖 Citation

If you found this library useful in your research and use this code in a publication or project, please cite:

```bibtex
@article{li2025x,
  title= {X's Day: Personality-Driven Virtual Human Behavior Generation},
  author = {Li, Haoyang and Wang, Zan and Liang, Wei and Wang, Yizhuo},
  journal = {IEEE Transactions on Visualization and Computer Graphics (TVCG)},
  year = {2025},
  publisher = {IEEE}
}
```
