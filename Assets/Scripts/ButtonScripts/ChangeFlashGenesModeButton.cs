﻿using UnityEngine;
/// <summary>
/// Represents the buttons that increase and decrease the number of frames between each gene expression when flashing genes.
/// </summary>
class ChangeFlashGenesModeButton : CellexalButton
{
    protected override string Description
    {
        get { return "Change the mode"; }
    }

    public CellManager.FlashGenesMode switchToMode;
    public StopButton stop;

    private CellManager cellManager;

    private void Start()
    {
        cellManager = referenceManager.cellManager;
    }

    public override void Click()
    {
        //infoCanv.SetActive(true);
        cellManager.CurrentFlashGenesMode = switchToMode;
        stop.SetButtonActivated(true);
        //stop.spriteRenderer.sprite = stop.standardTexture;
    }
}
