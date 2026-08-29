using System.Windows.Controls;

namespace Partcounter;

public partial class MainWindow
{
    internal TabControl MainTabsForLayoutValidation => MainTabs;
    internal TabControl? AdministrationTabsForLayoutValidation => _administrationTabs;
    internal bool AdministrationHubReadyForLayoutValidation => _administrationTab is not null && _administrationTabs is not null;

    internal void SelectMainTabForLayoutValidation(TabItem tab)
    {
        _tabGuardBusy = true;
        try
        {
            MainTabs.SelectedItem = tab;
        }
        finally
        {
            _tabGuardBusy = false;
        }
    }
}
