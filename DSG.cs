// DSG.cs
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
using System;
using System.Collections.Generic;
using UnityEngine;

/// 
/// Represents a node in the dynamic scene graph (DSG).
/// 
[Serializable]
public class Node
{
    ///  Unique identifier (e.g. scene object name) 
    public string ID;

    ///  Type of node: "object", "place", or "room" 
    public string Type;

    ///  Position in world space (center for rooms/places, exact for objects) 
    public Vector3 Position;

    ///  Rotation (currently unused) 
    public Quaternion Rotation;

    ///  Size of bounding area (rooms/places) or object bounds 
    public Vector3 Scale;

    /// 
    /// Initializes a new instance of the Node class.
    /// 
    public Node(string id, string type, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        ID = id;
        Type = type;
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    /// 
    /// Returns true if this room fully contains the given place on the XZ plane.
    /// 
    public bool IsPlaceWithinRoom(Node placeNode) {
        Vector3 placeMin = placeNode.Position - placeNode.Scale / 2;
        Vector3 placeMax = placeNode.Position + placeNode.Scale / 2;
        Vector3 roomMin = Position - Scale / 2;
        Vector3 roomMax = Position + Scale / 2;

        return (placeMin.x >= roomMin.x && placeMax.x <= roomMax.x &&
                placeMin.z >= roomMin.z && placeMax.z <= roomMax.z);
    }

    /// 
    /// Returns true if this node overlaps at least 75% of the other node's XZ area.
    /// 
    public bool OverlapsWith(Node other) {
        Vector3 thisMin = Position - Scale / 2;
        Vector3 thisMax = Position + Scale / 2;
        Vector3 otherMin = other.Position - other.Scale / 2;
        Vector3 otherMax = other.Position + other.Scale / 2;

        float overlapX = Mathf.Max(0, Mathf.Min(thisMax.x, otherMax.x) - Mathf.Max(thisMin.x, otherMin.x));
        float overlapZ = Mathf.Max(0, Mathf.Min(thisMax.z, otherMax.z) - Mathf.Max(thisMin.z, otherMin.z));

        float overlapArea = overlapX * overlapZ;
        float otherArea = other.Scale.x * other.Scale.z;
        
        return (overlapArea / otherArea) >= 0.75f;
    }
}

/// 
/// Represents an edge between two nodes in the DSG.
/// 
[Serializable]
public class Edge
{
    ///  Starting node of the edge 
    public Node StartNode;

    ///  Ending node of the edge 
    public Node EndNode;

    ///  "containment" or "traversability" 
    public string Type;

    ///  True if directed (containment), false if undirected (traversability) 
    public bool IsDirected;

    /// 
    /// Initializes a new instance of the Edge class.
    /// 
    public Edge(Node startNode, Node endNode, string type, bool isDirected)
    {
        StartNode = startNode;
        EndNode   = endNode;
        Type      = type;
        IsDirected = isDirected;
    }
}

/// 
/// Manages a dynamic scene graph composed of nodes and edges.
/// 
[Serializable]
public class DSG
{
    public List<Node> Nodes = new List<Node>();
    public List<Edge> Edges = new List<Edge>();

    /// 
    /// Adds a node to the graph.
    /// 
    public void AddNode(Node node)
    {
        Nodes.Add(node);
    }

    /// 
    /// Adds an edge, ensuring traversability only between place nodes.
    /// 
    public void AddEdge(Edge edge) {
        if (edge.Type == "traversability" && (edge.StartNode.Type != "place" || edge.EndNode.Type != "place")) {
            Debug.LogError("Traversability edges can only connect place nodes.");
            return;
        }
        Edges.Add(edge);
    }
    
    public void RemoveEdge(Edge edge)
    {
        Edges.Remove(edge);
    }

    /// 
    /// Returns all neighbors of the given node.
    /// 
    public List<Node> GetNeighbors(Node node) {
        List<Node> neighbors = new List<Node>();
        foreach (Edge edge in Edges) {
            if (edge.StartNode == node) {
                neighbors.Add(edge.EndNode);
            }
            if (!edge.IsDirected && edge.EndNode == node) {
                neighbors.Add(edge.StartNode);
            }
        }
        return neighbors;
    }

    /// 
    /// Finds the place node that contains the given object node.
    /// 
    public Node FindContainingPlace(Node objNode) {
        foreach (Node place in Nodes) {
            if (place.Type == "place" && place.OverlapsWith(objNode)) {
                return place;
            }
        }
        return null;
    }

    /// 
    /// Updates containment edges for all object nodes in the graph.
    /// 
    public void UpdateAllDynamicObjects() 
    {
        foreach (Node obj in Nodes) 
        {
            if (obj.Type == "object") 
            {
                UpdateOneObject(obj);
            }
        }
    }

    /// 
    /// Updates the containment edge for a single object node.
    /// 
    public void UpdateOneObject(Node objNode) 
    {
        Node currentPlace = FindContainingPlace(objNode);
        if (currentPlace != null) 
        {
            Edge currentEdge = Edges.Find(edge => edge.StartNode == currentPlace && edge.EndNode == objNode && edge.Type == "containment" && edge.IsDirected);
            if (currentEdge == null) 
            {
                Edge previousEdge = Edges.Find(edge => edge.EndNode == objNode && edge.Type == "containment" && edge.IsDirected);
                Node previousPlace = previousEdge?.StartNode;
                if (previousPlace != null) 
                {
                    RemoveEdge(previousEdge);
                }
                AddEdge(new Edge(currentPlace, objNode, "containment", true));
            }
        }
    }
}