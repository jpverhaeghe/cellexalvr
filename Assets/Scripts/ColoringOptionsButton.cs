﻿using UnityEngine;

/// <summary>
/// This class represents a button that can choose between a <see cref="GraphManager.GeneExpressionColoringMethods"/>
/// </summary>
public class ColoringOptionsButton : ClickablePanel
{
    public GraphManager.GeneExpressionColoringMethods modeToSwitchTo;

    private GraphManager graphManager;

    protected override void Start()
    {
        base.Start();
        graphManager = referenceManager.graphManager;
    }

    public override void Click()
    {
        graphManager.GeneExpressionColoringMethod = modeToSwitchTo;
        // set all other texts to white and ours to green
        foreach (TextMesh textMesh in transform.parent.gameObject.GetComponentsInChildren<TextMesh>())
        {
            textMesh.color = Color.white;
        }
        GetComponentInChildren<TextMesh>().color = Color.green;
    }
}

