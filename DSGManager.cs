// DSGManager.cs
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
using System.Collections.Generic;
using UnityEngine;

/// 
/// Manages the Dynamic Scene Graph (DSG) instance and optionally visualizes it in the Scene view.
/// 
public class DSGManager : MonoBehaviour
{
    /// The DSG instance holding nodes and edges. 
    public DSG SceneGraph;

    /// Toggle to draw nodes and edges as Gizmos in the editor.
    public bool isDraw = false;

    /// 
    /// Initialize the SceneGraph if not already assigned.
    /// 
    private void Awake()
    {
        if (SceneGraph == null)
        {
            SceneGraph = new DSG();
        }
    }

    /// 
    /// Draws nodes and edges in the Scene view when isDraw is enabled.
    /// 
    private void OnDrawGizmos()
    {
        if (!isDraw || SceneGraph == null) 
            return;

        // Draw each node as a colored cube
        foreach (Node node in SceneGraph.Nodes) {
            Gizmos.color = node.Type == "room" ? Color.blue : (node.Type == "place" ? Color.gray : Color.green);
            Gizmos.DrawCube(node.Position, node.Scale);
        }

        // Draw each edge as a line (and arrowheads for containment)
        foreach (Edge edge in SceneGraph.Edges) {
            if (edge.Type == "containment") {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(edge.StartNode.Position, edge.EndNode.Position);
                // Draw arrow for directed edge
                Vector3 direction = (edge.EndNode.Position - edge.StartNode.Position).normalized;
                Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
                Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
                Gizmos.DrawLine(edge.EndNode.Position, edge.EndNode.Position + right * 0.5f);
                Gizmos.DrawLine(edge.EndNode.Position, edge.EndNode.Position + left * 0.5f);
            } else {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(edge.StartNode.Position, edge.EndNode.Position);
            }
        }
    }
}