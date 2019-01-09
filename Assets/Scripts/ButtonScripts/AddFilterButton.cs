﻿
public class AddFilterButton : CellexalButton
{
    protected override string Description
    {
        get { return "Add the above filter"; }
    }

    private NewFilterMenu newFilterMenu;

    private void Start()
    {
        newFilterMenu = referenceManager.newFilterMenu;
    }


    public override void Click()
    {
        newFilterMenu.AddFilter();
    }
}
