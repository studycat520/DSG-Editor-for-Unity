// DSGEditor.cs
// Copyright 2025 Haoyang Li
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// 
/// Component attached to placeholder GameObjects to mark DSG nodes in the scene.
/// 
public class NodeComponent : MonoBehaviour
{
    public enum NodeType { Room, Place, Object }
    public NodeType Type;
    public string ID;
}

/// 
/// Container for the generated scene description JSON.
/// 
public class SceneDescription
{
    public string description;
}

/// 
/// Custom Editor window for creating and managing a Dynamic Scene Graph (DSG).
/// 
public class DSGEditor : EditorWindow
{
    // Ground plane at y = 0, used for rectangle selection
    private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

    // Node creation state
    private string nodeName = "";
    private NodeComponent.NodeType selectedNodeType;
    private GameObject selectedObjectForNode;

    // Range selection state
    private bool selectingRange, rangeSelected, isDragging;
    private Vector3 startPoint, endPoint, rangeCenter, rangeSize, dragOffset;

    // Scroll area
    private Vector2 scrollPosition;

    // Edge creation/editing state
    private List<Node> validContainmentStartNodes, validContainmentEndNodes, validTraversabilityNodes;
    private int newContainmentStartNodeIndex=0, newContainmentEndNodeIndex=0;
    private int newTraversabilityStartNodeIndex=0, newTraversabilityEndNodeIndex=0;
    private Edge edgeBeingEdited = null;
    private string editEdgeType = "containment";
    private int editStartNodeIndex=0, editEndNodeIndex=0;
    

    private DSGManager dsgManager;

    [MenuItem("Window/DSG Editor")]
    public static void ShowWindow()
    {
        GetWindow<DSGEditor>("DSG Editor");
    }

    /// 
    /// Called when the window is enabled; initialize references and subscribe to playmode changes.
    /// 
    private void OnEnable()
    {
        if (dsgManager == null)
        {
            dsgManager = FindObjectOfType<DSGManager>();
        }

        if (dsgManager != null)
        {
            LoadDataFromDSGManager();
        }
        
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    /// 
    /// Called when the window is disabled; clear references and unsubscribe.
    /// 
    private void OnDisable()
    {
        validContainmentStartNodes = null;
        validContainmentEndNodes = null;
        validTraversabilityNodes = null;
        
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    /// 
    /// Reloads the lists of valid nodes for containment and traversability operations.
    /// 
    private void LoadDataFromDSGManager()
    {
        validContainmentStartNodes = GetValidContainmentStartNodes();
        validContainmentEndNodes = GetValidContainmentEndNodes();
        validTraversabilityNodes = GetValidTraversabilityNodes();
    }
    
    private List<Node> GetValidContainmentStartNodes()
    {
        List<Node> validStartNodes = new List<Node>();

        foreach (var node in dsgManager.SceneGraph.Nodes)
        {
            if (node.Type == "room" || node.Type == "place")
            {
                validStartNodes.Add(node);
            }
        }

        return validStartNodes;
    }

    private List<Node> GetValidContainmentEndNodes()
    {
        List<Node> validEndNodes = new List<Node>();

        foreach (var node in dsgManager.SceneGraph.Nodes)
        {
            if (node.Type == "place" || node.Type == "object")
            {
                validEndNodes.Add(node);
            }
        }

        return validEndNodes;
    }


    private List<Node> GetValidTraversabilityNodes()
    {
        List<Node> validNodes = new List<Node>();

        foreach (var node in dsgManager.SceneGraph.Nodes)
        {
            if (node.Type == "place")
            {
                validNodes.Add(node);
            }
        }

        return validNodes;
    }

    /// 
    /// Responds to playmode state changes to refresh data.
    /// 
    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
        {
            LoadDataFromDSGManager();
        }
        else if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
        {
            validContainmentStartNodes = null;
            validContainmentEndNodes = null;
            validTraversabilityNodes = null;
        }
    }

    /// 
    /// Main GUI rendering.
    /// 
    private void OnGUI() {
        if (dsgManager == null)
        {
            dsgManager = FindObjectOfType<DSGManager>();
            if (dsgManager != null)
            {
                LoadDataFromDSGManager();
                Debug.Log("DSGManager's info is Load!");
            }
        }
        GUILayout.Label("DSG Node Configuration", EditorStyles.boldLabel);

        selectedNodeType = (NodeComponent.NodeType)EditorGUILayout.EnumPopup("Select Node Type", selectedNodeType);

        if (selectedNodeType == NodeComponent.NodeType.Room || selectedNodeType == NodeComponent.NodeType.Place) {
            nodeName = EditorGUILayout.TextField("Node Name", nodeName);

            if (GUILayout.Button("Select Range (Shift+LeftDrag) for " + selectedNodeType)) {
                StartRangeSelection();
            }

            if (rangeSelected) {
                EditorGUILayout.LabelField("Range Center", rangeCenter.ToString());
                EditorGUILayout.LabelField("Range Size", rangeSize.ToString());
            }
        } else if (selectedNodeType == NodeComponent.NodeType.Object) {
            selectedObjectForNode = EditorGUILayout.ObjectField("Select Object", selectedObjectForNode, typeof(GameObject), true) as GameObject;
        }

        if (GUILayout.Button("Add Node to DSG")) {
            AddNodeToDSG();
        }

        if (GUILayout.Button("Generate Containment Edges")) {
            GenerateContainmentEdges();
        }

        if (GUILayout.Button("Generate Natural Language Description")) {
            GenerateNaturalLanguageDescription();
        }

        if (GUILayout.Button("Check Unconnected Nodes"))
        {
            CheckUnconnectedNodes();
        }



        GUILayout.Space(20);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height - 150));
        DisplayNodesAndEdges();
        EditorGUILayout.EndScrollView();
    }

    /// 
    /// Begins rectangle selection in the Scene view.
    /// 
    private void StartRangeSelection() {
        selectingRange = true;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// 
    /// Handles Scene view events for range selection and dragging.
    /// 
    private void OnSceneGUI(SceneView sceneView) {
        Event e = Event.current;

        if (selectingRange) {
            if (e.type == EventType.MouseDown && e.button == 0 && e.shift) {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (groundPlane.Raycast(ray, out float enter)) {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    startPoint = new Vector3(hitPoint.x, 0, hitPoint.z);
                    endPoint = startPoint;
                    e.Use();
                }
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && e.shift) {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (groundPlane.Raycast(ray, out float enter)) {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    endPoint = new Vector3(hitPoint.x, 0, hitPoint.z);
                    SceneView.RepaintAll();
                    e.Use();
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0 && e.shift) {
                CompleteRangeSelection();
                SceneView.duringSceneGui -= OnSceneGUI;
                selectingRange = false;
                e.Use();
            }

            if (selectingRange) {
                DrawSelectionRectangle();
            }
        } else if (rangeSelected) {
            HandleRangeModification(e);
        }
    }

    /// 
    /// Finalizes range selection and calculates center and size.
    /// 
    private void CompleteRangeSelection() {
        rangeCenter = (startPoint + endPoint) / 2;
        rangeSize = new Vector3(Mathf.Abs(startPoint.x - endPoint.x), 1, Mathf.Abs(startPoint.z - endPoint.z));

        rangeSelected = true;

        Debug.Log($"Range selected: Center = {rangeCenter}, Size = {rangeSize}");
    }

    /// 
    /// Draws the current selection rectangle in Scene view.
    /// 
    private void DrawSelectionRectangle() {
        Vector3 center = (startPoint + endPoint) / 2;
        Vector3 size = new Vector3(Mathf.Abs(startPoint.x - endPoint.x), 1, Mathf.Abs(startPoint.z - endPoint.z));
        Handles.color = Color.red;
        Handles.DrawWireCube(center, size);
    }
    
    private void HandleRangeModification(Event e) {
        Vector3 center = rangeCenter;
        Vector3 size = rangeSize;

        // Draw the current range
        Handles.color = Color.red;
        Handles.DrawWireCube(center, size);

        // Check for drag start
        if (e.type == EventType.MouseDown && e.button == 0) {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (groundPlane.Raycast(ray, out float enter)) {
                Vector3 hitPoint = ray.GetPoint(enter);
                hitPoint = new Vector3(hitPoint.x, 0, hitPoint.z);
                if (IsPointInRange(hitPoint, center, size)) {
                    isDragging = true;
                    dragOffset = hitPoint - center;
                    e.Use();
                }
            }
        }

        // Handle dragging
        if (e.type == EventType.MouseDrag && e.button == 0 && isDragging) {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (groundPlane.Raycast(ray, out float enter)) {
                Vector3 hitPoint = ray.GetPoint(enter);
                hitPoint = new Vector3(hitPoint.x, 0, hitPoint.z);
                rangeCenter = hitPoint - dragOffset;
                SceneView.RepaintAll();
                e.Use();
            }
        }

        // End drag
        if (e.type == EventType.MouseUp && e.button == 0 && isDragging) {
            isDragging = false;
            e.Use();
        }
    }
    
    private bool IsPointInRange(Vector3 point, Vector3 center, Vector3 size) {
        return (point.x >= center.x - size.x / 2 && point.x <= center.x + size.x / 2 &&
                point.z >= center.z - size.z / 2 && point.z <= center.z + size.z / 2);
    }

    /// 
    /// Adds a new node to the DSG based on the current GUI inputs.
    /// 
    private void AddNodeToDSG() {
        if (selectedNodeType == NodeComponent.NodeType.Room || selectedNodeType == NodeComponent.NodeType.Place) {
            if (string.IsNullOrEmpty(nodeName)) {
                Debug.LogError("Node name cannot be empty.");
                return;
            }

            if (!rangeSelected) {
                Debug.LogError("You must select a range before adding a room or place node.");
                return;
            }
        } else if (selectedNodeType == NodeComponent.NodeType.Object) {
            if (selectedObjectForNode == null) {
                Debug.LogError("You must select an object for the object node.");
                return;
            }
            nodeName = selectedObjectForNode.name;
        }

        if (dsgManager == null) {
            dsgManager = FindObjectOfType<DSGManager>();
        }

        Vector3 position = Vector3.zero;
        Vector3 scale = Vector3.one;

        if (rangeSelected) {
            position = rangeCenter;
            scale = rangeSize;
        } else if (selectedObjectForNode != null) {
            Bounds bounds = GetObjectBounds(selectedObjectForNode);
            position = bounds.center;
            scale = bounds.size;
        }

        Node node = new Node(nodeName, selectedNodeType.ToString().ToLower(), position, Quaternion.identity, scale);
        dsgManager.SceneGraph.AddNode(node);

        Debug.Log($"{selectedNodeType} node added to DSG: {nodeName}");

        // reset
        rangeSelected = false;
        rangeCenter = Vector3.zero;
        rangeSize = Vector3.zero;
        selectedObjectForNode = null;
    }

    /// 
    /// Automatically generates containment edges based on overlaps and containment rules.
    /// 
    private void GenerateContainmentEdges()
    {
        LoadDataFromDSGManager(); 

        var currentNodes = GetCurrentNodes(); 

        foreach (Node placeNode in currentNodes)
        {
            if (placeNode.Type == "place")
            {
                foreach (Node objNode in currentNodes)
                {
                    if (objNode.Type == "object" && placeNode.OverlapsWith(objNode))
                    {
                        if (!EdgeExists(placeNode, objNode, "containment"))
                        {
                            dsgManager.SceneGraph.AddEdge(new Edge(placeNode, objNode, "containment", true));
                        }
                    }
                }

                foreach (Node roomNode in currentNodes)
                {
                    if (roomNode.Type == "room" && roomNode.IsPlaceWithinRoom(placeNode))
                    {
                        if (!EdgeExists(roomNode, placeNode, "containment"))
                        {
                            dsgManager.SceneGraph.AddEdge(new Edge(roomNode, placeNode, "containment", true));
                        }
                    }
                }
            }
        }

        Debug.Log("Containment edges generated.");
    }
    
    //get Current Nodes/Edges in DSGManager
    private List<Node> GetCurrentNodes()
    {
        return dsgManager.SceneGraph.Nodes.ToList();
    }
    private List<Edge> GetCurrentEdges()
    {
        return dsgManager.SceneGraph.Edges.ToList(); 
    }

    /// 
    /// Checks for and logs any nodes that have no connecting edges.
    /// 
    private void CheckUnconnectedNodes()
    {
        bool allConnected = true;
        HashSet<string> connectedNodeIDs = new HashSet<string>();
        
        foreach (var edge in dsgManager.SceneGraph.Edges)
        {
            connectedNodeIDs.Add(edge.StartNode.ID);
            connectedNodeIDs.Add(edge.EndNode.ID);
        }
        
        foreach (var node in dsgManager.SceneGraph.Nodes)
        {
            if (!connectedNodeIDs.Contains(node.ID))
            {
                allConnected=false;
                Debug.Log($"Unconnected Node - Type: {node.Type}, ID: {node.ID}");
            }
        }

        if (allConnected)
        {
            Debug.Log($"All Node is connected!");
        }
    }

    /// 
    /// Displays existing nodes and edges, with options to remove or edit.
    /// 
    private void DisplayNodesAndEdges() {
        if (dsgManager == null) {
            dsgManager = FindObjectOfType<DSGManager>();
        }

        if (dsgManager == null) return;

        GUILayout.Label("Current Nodes and Edges", EditorStyles.boldLabel);

        GUILayout.Label("Room Nodes", EditorStyles.boldLabel);
        foreach (Node node in dsgManager.SceneGraph.Nodes.ToList()) {
            if (node.Type == "room") {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"ID: {node.ID}");
                if (GUILayout.Button("Remove")) {
                    RemoveNode(node);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        GUILayout.Label("Place Nodes", EditorStyles.boldLabel);
        foreach (Node node in dsgManager.SceneGraph.Nodes.ToList()) {
            if (node.Type == "place") {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"ID: {node.ID}");
                if (GUILayout.Button("Remove")) {
                    RemoveNode(node);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        GUILayout.Label("Object Nodes", EditorStyles.boldLabel);
        foreach (Node node in dsgManager.SceneGraph.Nodes.ToList()) {
            if (node.Type == "object") {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"ID: {node.ID}");
                if (GUILayout.Button("Remove")) {
                    RemoveNode(node);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Containment Edges", EditorStyles.boldLabel);
        foreach (Edge edge in dsgManager.SceneGraph.Edges.ToList()) {
            if (edge.Type == "containment") {
                if (edgeBeingEdited == edge) {
                    EditorGUILayout.BeginHorizontal();
                    editStartNodeIndex = EditorGUILayout.Popup("Start Node", editStartNodeIndex, validContainmentStartNodes.Select(n => n.ID).ToArray());
                    editEndNodeIndex = EditorGUILayout.Popup("End Node", editEndNodeIndex, validContainmentEndNodes.Select(n => n.ID).ToArray());
                    editEdgeType = EditorGUILayout.Popup("Edge Type", editEdgeType == "containment" ? 0 : 1, new string[] { "containment", "traversability" }) == 0 ? "containment" : "traversability";
                    if (GUILayout.Button("Done")) {
                        DoneEditingEdge(edge);
                    }
                    EditorGUILayout.EndHorizontal();
                } else {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Start: {edge.StartNode.ID}, End: {edge.EndNode.ID}");
                    if (GUILayout.Button("Edit")) {
                        StartEditingEdge(edge);
                    }
                    if (GUILayout.Button("Remove")) {
                        RemoveEdge(edge);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // Add new containment edge
        GUILayout.Space(10);
        GUILayout.Label("Add New Containment Edge", EditorStyles.boldLabel);
        validContainmentStartNodes = dsgManager.SceneGraph.Nodes.Where(n => n.Type == "room" || n.Type == "place").ToList();
        validContainmentEndNodes = dsgManager.SceneGraph.Nodes.Where(n => n.Type == "place" || n.Type == "object").ToList();
        
        string[] startNodeOptions = validContainmentStartNodes.Select(n => n.ID).ToArray();
        string[] endNodeOptions = validContainmentEndNodes.Select(n => n.ID).ToArray();

        newContainmentStartNodeIndex = EditorGUILayout.Popup("Start Node", newContainmentStartNodeIndex, startNodeOptions);
        newContainmentEndNodeIndex = EditorGUILayout.Popup("End Node", newContainmentEndNodeIndex, endNodeOptions);
        
        if (GUILayout.Button("Add Containment Edge")) {
            AddContainmentEdge();
        }

        GUILayout.Label("Traversability Edges", EditorStyles.boldLabel);
        foreach (Edge edge in dsgManager.SceneGraph.Edges.ToList()) {
            if (edge.Type == "traversability") {
                if (edgeBeingEdited == edge) {
                    EditorGUILayout.BeginHorizontal();
                    editStartNodeIndex = EditorGUILayout.Popup("Start Node", editStartNodeIndex, validTraversabilityNodes.Select(n => n.ID).ToArray());
                    editEndNodeIndex = EditorGUILayout.Popup("End Node", editEndNodeIndex, validTraversabilityNodes.Select(n => n.ID).ToArray());
                    editEdgeType = EditorGUILayout.Popup("Edge Type", editEdgeType == "containment" ? 0 : 1, new string[] { "containment", "traversability" }) == 0 ? "containment" : "traversability";
                    if (GUILayout.Button("Done")) {
                        DoneEditingEdge(edge);
                    }
                    EditorGUILayout.EndHorizontal();
                } else {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Start: {edge.StartNode.ID}, End: {edge.EndNode.ID}");
                    if (GUILayout.Button("Edit")) {
                        StartEditingEdge(edge);
                    }
                    if (GUILayout.Button("Remove")) {
                        RemoveEdge(edge);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // Add new traversability edge
        GUILayout.Space(10);
        GUILayout.Label("Add New Traversability Edge", EditorStyles.boldLabel);
        validTraversabilityNodes = dsgManager.SceneGraph.Nodes.Where(n => n.Type == "place").ToList();
        
        string[] traversabilityNodeOptions = validTraversabilityNodes.Select(n => n.ID).ToArray();

        newTraversabilityStartNodeIndex = EditorGUILayout.Popup("Start Node", newTraversabilityStartNodeIndex, traversabilityNodeOptions);
        newTraversabilityEndNodeIndex = EditorGUILayout.Popup("End Node", newTraversabilityEndNodeIndex, traversabilityNodeOptions);

        if (GUILayout.Button("Add Traversability Edge")) {
            AddTraversabilityEdge();
        }
        GUILayout.Space(20);
    }
    //Edit Edge
    private void StartEditingEdge(Edge edge) {
        edgeBeingEdited = edge;
        editEdgeType = edge.Type;
        if (editEdgeType == "containment") {
            validContainmentStartNodes = dsgManager.SceneGraph.Nodes.Where(n => n.Type == "room" || n.Type == "place").ToList();
            validContainmentEndNodes = dsgManager.SceneGraph.Nodes.Where(n => n.Type == "place" || n.Type == "object").ToList();
            editStartNodeIndex = validContainmentStartNodes.IndexOf(edge.StartNode);
            editEndNodeIndex = validContainmentEndNodes.IndexOf(edge.EndNode);
        } else {
            validTraversabilityNodes = dsgManager.SceneGraph.Nodes.Where(n => n.Type == "place").ToList();
            editStartNodeIndex = validTraversabilityNodes.IndexOf(edge.StartNode);
            editEndNodeIndex = validTraversabilityNodes.IndexOf(edge.EndNode);
        }
    }
    private void DoneEditingEdge(Edge edge) {
        Node newStartNode = (editEdgeType == "containment") ? validContainmentStartNodes[editStartNodeIndex] : validTraversabilityNodes[editStartNodeIndex];
        Node newEndNode = (editEdgeType == "containment") ? validContainmentEndNodes[editEndNodeIndex] : validTraversabilityNodes[editEndNodeIndex];

        if ((editEdgeType == "containment" && IsValidContainmentEdge(newStartNode, newEndNode)) ||
            (editEdgeType == "traversability" && IsValidTraversabilityEdge(newStartNode, newEndNode))) {
            edge.StartNode = newStartNode;
            edge.EndNode = newEndNode;
            edge.Type = editEdgeType;
            edgeBeingEdited = null;
        } else {
            Debug.LogError("Invalid edge type or node types do not match.");
        }
    }
    
    //Add Two Edge Type
    private void AddContainmentEdge()
    {
        LoadDataFromDSGManager();

        Node startNode = validContainmentStartNodes[newContainmentStartNodeIndex];
        Node endNode = validContainmentEndNodes[newContainmentEndNodeIndex];

        if (EdgeExists(startNode, endNode, "containment"))
        {
            Debug.LogError("Containment edge already exists.");
            return;
        }

        if (startNode != null && endNode != null && IsValidContainmentEdge(startNode, endNode))
        {
            dsgManager.SceneGraph.AddEdge(new Edge(startNode, endNode, "containment", true));
        }
        else
        {
            Debug.LogError("Invalid edge type or node types do not match.");
        }

        newContainmentStartNodeIndex = 0;
        newContainmentEndNodeIndex = 0;
    }
    
    private void AddTraversabilityEdge()
    {
        LoadDataFromDSGManager();

        Node startNode = validTraversabilityNodes[newTraversabilityStartNodeIndex];
        Node endNode = validTraversabilityNodes[newTraversabilityEndNodeIndex];

        if (EdgeExists(startNode, endNode, "traversability"))
        {
            Debug.LogError("Traversability edge already exists.");
            return;
        }

        if (startNode != null && endNode != null && IsValidTraversabilityEdge(startNode, endNode))
        {
            dsgManager.SceneGraph.AddEdge(new Edge(startNode, endNode, "traversability", false));
        }
        else
        {
            Debug.LogError("Invalid edge type or node types do not match.");
        }

        newTraversabilityStartNodeIndex = 0;
        newTraversabilityEndNodeIndex = 0;
    }

    private void RemoveNode(Node node) {
        List<Edge> edgesToRemove = new List<Edge>();
        foreach (Edge edge in dsgManager.SceneGraph.Edges) {
            if (edge.StartNode == node || edge.EndNode == node) {
                edgesToRemove.Add(edge);
            }
        }
        
        foreach (Edge edge in edgesToRemove) {
            dsgManager.SceneGraph.Edges.Remove(edge);
        }
        
        dsgManager.SceneGraph.Nodes.Remove(node);
    }
    
    private void RemoveEdge(Edge edge) {
        dsgManager.SceneGraph.Edges.Remove(edge);
    }
    
    //Edge Validation
    private bool IsValidContainmentEdge(Node startNode, Node endNode) {
        return (startNode.Type == "room" && endNode.Type == "place") ||
               (startNode.Type == "place" && endNode.Type == "object");
    }

    private bool IsValidTraversabilityEdge(Node startNode, Node endNode) {
        return startNode.Type == "place" && endNode.Type == "place";
    }
    
    /// 
    /// Determines whether an edge of the given type already exists between two nodes.
    /// 
    private bool EdgeExists(Node startNode, Node endNode, string type)
    {
        var currentEdges = GetCurrentEdges();

        foreach (var edge in currentEdges)
        {
            if (edge.Type == type)
            {
                if (type == "containment")
                {
                    if (edge.StartNode == startNode && edge.EndNode == endNode)
                    {
                        return true;
                    }
                }
                else if (type == "traversability")
                {
                    if ((edge.StartNode == startNode && edge.EndNode == endNode) ||
                        (edge.StartNode == endNode && edge.EndNode == startNode))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// 
    /// Generates a natural-language JSON description and writes to Assets/SceneDescription.json.
    /// 
    private void GenerateNaturalLanguageDescription()
    {
        if (dsgManager == null)
        {
            Debug.LogError("DSGManager is not assigned.");
            return;
        }

        List<string> descriptions = new List<string>();
        Dictionary<string, Node> rooms = new Dictionary<string, Node>();
        Dictionary<string, List<Node>> roomPlaces = new Dictionary<string, List<Node>>();
        Dictionary<string, List<Node>> placeObjects = new Dictionary<string, List<Node>>();
        List<string> traversabilityEdges = new List<string>();
        
        foreach (var node in dsgManager.SceneGraph.Nodes)
        {
            if (node.Type == "room")
            {
                rooms[node.ID] = node;
                roomPlaces[node.ID] = new List<Node>();
            }
            else if (node.Type == "place")
            {
                placeObjects[node.ID] = new List<Node>();
            }
        }
        
        foreach (var edge in dsgManager.SceneGraph.Edges)
        {
            if (edge.Type == "containment")
            {
                if (rooms.ContainsKey(edge.StartNode.ID) && roomPlaces.ContainsKey(edge.StartNode.ID))
                {
                    roomPlaces[edge.StartNode.ID].Add(edge.EndNode);
                }
                else if (placeObjects.ContainsKey(edge.StartNode.ID))
                {
                    placeObjects[edge.StartNode.ID].Add(edge.EndNode);
                }
            }
            else if (edge.Type == "traversability")
            {
                traversabilityEdges.Add($"The walking areas between [{edge.StartNode.ID}] and [{edge.EndNode.ID}] are interconnected.");
            }
        }

        // Generate Language Description
        foreach (var room in rooms.Values)
        {
            string roomDescription = $"The house has the room that named {{{room.ID}}}.";
            List<string> placeDescriptions = new List<string>();

            if (roomPlaces.ContainsKey(room.ID))
            {
                foreach (var place in roomPlaces[room.ID])
                {
                    string placeDescription = $"In {{{room.ID}}}, there is a place called [{place.ID}].";
                    if (placeObjects.ContainsKey(place.ID))
                    {
                        var objects = placeObjects[place.ID];
                        string objectDescription;
                        if (objects.Count==0) {objectDescription="";}
                        else
                        {objectDescription = $"In [{place.ID}], there are {objects.Count} objects, called {string.Join(" and ", objects.Select(o => $"({o.ID})"))}.";}
                        placeDescriptions.Add(placeDescription + " " + objectDescription);
                    }
                    else
                    {
                        placeDescriptions.Add(placeDescription);
                    }
                }
            }

            descriptions.Add(roomDescription + string.Join(" ", placeDescriptions) + "\n");
        }

        string connectivityDescription = "The connectivity information between places is as follows: " + string.Join(" ", traversabilityEdges);
        descriptions.Add(connectivityDescription);

        SceneDescription sceneDescription = new SceneDescription
        {
            description = string.Join(" ", descriptions)
        };

        string json = JsonUtility.ToJson(sceneDescription, true);
        string path = Path.Combine(Application.dataPath, "SceneDescription.json");
        System.IO.File.WriteAllText(path, json);

        Debug.Log("Scene description saved to " + path);
    }

    /// 
    /// Computes the world-space bounds of a GameObject by encapsulating all child Renderers.
    /// 
    private Bounds GetObjectBounds(GameObject obj) {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }
}