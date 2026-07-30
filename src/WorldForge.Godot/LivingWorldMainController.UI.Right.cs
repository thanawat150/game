using System.Diagnostics;
using System.Text.Json;
using Godot;
using WorldForge.Core.Editing;
using WorldForge.Core.Persistence;
using WorldForge.Core.Simulation;
using WorldForge.Core.World;
using WorldForge.Presentation;

namespace WorldForge;

public sealed partial class LivingWorldMainController : Node2D
{
    private void BuildRightPanel()
    {
        var panel = new PanelContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = -400,
            OffsetTop = 66,
            OffsetRight = -8,
            OffsetBottom = -40,
        };
        _gameLayer.AddChild(panel);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panel.AddChild(scroll);
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(370, 0) };
        root.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(root);

        root.AddChild(Section("Inspector"));
        _inspectorLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 150) };
        root.AddChild(_inspectorLabel);
        _renameInput = new LineEdit { PlaceholderText = "ชื่อใหม่" };
        root.AddChild(_renameInput);
        root.AddChild(CreateButton("เปลี่ยนชื่อสิ่งที่เลือก", RenameSelected));

        root.AddChild(new HSeparator());
        root.AddChild(Section("บริหารเมือง"));
        _cityPriority = AddEnumOption<CityPriority>(root, PriorityThai);
        _cityBorder = AddEnumOption<BorderPolicy>(root, BorderThai);
        _cityTax = AddSliderRow(root, "ภาษี", 0, 0.5, 0.01, 0.12);
        _cityBirth = AddSliderRow(root, "นโยบายการเกิด", 0, 2, 0.05, 1);
        _cityFoodReserve = AddSpinRow(root, "อาหารสำรองเป้าหมาย", 0, 2000, 10, 120);
        _cityPopulationLimit = AddSpinRow(root, "เพดานประชากรเมือง", 0, 3000, 10, 500);
        _cityAutoBuild = new CheckButton { Text = "ก่อสร้างอัตโนมัติ", ButtonPressed = true };
        _cityQuarantine = new CheckButton { Text = "กักกันโรค" };
        _cityEvacuate = new CheckButton { Text = "อพยพประชากร" };
        root.AddChild(_cityAutoBuild);
        root.AddChild(_cityQuarantine);
        root.AddChild(_cityEvacuate);
        foreach (Control control in new Control[] { _cityPriority, _cityBorder, _cityTax, _cityBirth, _cityFoodReserve, _cityPopulationLimit, _cityAutoBuild, _cityQuarantine, _cityEvacuate })
        {
            if (control is OptionButton option) option.ItemSelected += _ => ApplySelectedCityPolicy();
            else if (control is Range range) range.ValueChanged += _ => ApplySelectedCityPolicy();
            else if (control is BaseButton button) button.Toggled += _ => ApplySelectedCityPolicy();
        }
        var cityActions = new HBoxContainer();
        cityActions.AddChild(CreateButton("สร้างอาคาร", BuildSelectedCity));
        cityActions.AddChild(CreateButton("เทศกาล", FestivalSelectedCity));
        cityActions.AddChild(CreateButton("รักษาโรค", HealSelectedCity));
        root.AddChild(cityActions);

        root.AddChild(new HSeparator());
        root.AddChild(Section("บริหารอาณาจักร"));
        _kingdomBorder = AddEnumOption<BorderPolicy>(root, BorderThai);
        _kingdomTax = AddSliderRow(root, "ตัวคูณภาษี", 0.5, 2, 0.05, 1);
        _kingdomBirth = AddSliderRow(root, "นโยบายการเกิด", 0, 2, 0.05, 1);
        _kingdomMilitary = AddSliderRow(root, "ความสำคัญกองทัพ", 0, 1, 0.05, 0.5);
        _kingdomPopulationLimit = AddSpinRow(root, "เพดานประชากรอาณาจักร", 0, 6000, 25, 2500);
        _kingdomPreferPeace = new CheckButton { Text = "ให้ความสำคัญกับสันติ", ButtonPressed = true };
        root.AddChild(_kingdomPreferPeace);
        foreach (Control control in new Control[] { _kingdomBorder, _kingdomTax, _kingdomBirth, _kingdomMilitary, _kingdomPopulationLimit, _kingdomPreferPeace })
        {
            if (control is OptionButton option) option.ItemSelected += _ => ApplySelectedKingdomPolicy();
            else if (control is Range range) range.ValueChanged += _ => ApplySelectedKingdomPolicy();
            else if (control is BaseButton button) button.Toggled += _ => ApplySelectedKingdomPolicy();
        }

        root.AddChild(new HSeparator());
        root.AddChild(Section("Scenario และเป้าหมาย"));
        _scenarioSelector = new OptionButton();
        foreach (ScenarioKind scenario in Enum.GetValues<ScenarioKind>())
            _scenarioSelector.AddItem(ScenarioThai(scenario), (int)scenario);
        _scenarioSelector.ItemSelected += id =>
        {
            _director?.SelectScenario((ScenarioKind)(int)id);
            UpdateScenario();
        };
        root.AddChild(_scenarioSelector);
        _scenarioLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_scenarioLabel);
        _scenarioProgress = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 0, ShowPercentage = true };
        root.AddChild(_scenarioProgress);

        _eventPanel = new PanelContainer();
        root.AddChild(_eventPanel);
        var eventRoot = new VBoxContainer();
        _eventPanel.AddChild(eventRoot);
        _eventTitle = new Label { Text = "เหตุการณ์โลก" };
        _eventDescription = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        eventRoot.AddChild(_eventTitle);
        eventRoot.AddChild(_eventDescription);
        for (int i = 0; i < _eventChoiceButtons.Length; i++)
        {
            int choice = i;
            _eventChoiceButtons[i] = CreateButton($"ทางเลือก {i + 1}", () => ResolveEvent(choice));
            eventRoot.AddChild(_eventChoiceButtons[i]);
        }
        _eventPanel.Visible = false;

        root.AddChild(new HSeparator());
        root.AddChild(Section("Chronicle"));
        var filterRow = new HBoxContainer();
        _chronicleFilter = new OptionButton();
        foreach (ChronicleFilter filter in Enum.GetValues<ChronicleFilter>())
            _chronicleFilter.AddItem(FilterThai(filter), (int)filter);
        _chronicleFilter.ItemSelected += _ => RebuildChronicle();
        filterRow.AddChild(_chronicleFilter);
        _chronicleSearch = new LineEdit { PlaceholderText = "กรองเหตุการณ์", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _chronicleSearch.TextChanged += _ => RebuildChronicle();
        filterRow.AddChild(_chronicleSearch);
        root.AddChild(filterRow);
        _chronicleList = new ItemList { CustomMinimumSize = new Vector2(360, 230), SelectMode = ItemList.SelectModeEnum.Single };
        _chronicleList.ItemSelected += index => FocusChronicle((int)index);
        root.AddChild(_chronicleList);
    }
}
