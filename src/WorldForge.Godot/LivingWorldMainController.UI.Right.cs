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
        _renameInput = new LineEdit { PlaceholderText = "à¸Šà¸·à¹ˆà¸­à¹ƒà¸«à¸¡à¹ˆ" };
        root.AddChild(_renameInput);
        root.AddChild(CreateButton("à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™à¸Šà¸·à¹ˆà¸­à¸ªà¸´à¹ˆà¸‡à¸—à¸µà¹ˆà¹€à¸¥à¸·à¸­à¸", RenameSelected));

        root.AddChild(new HSeparator());
        root.AddChild(Section("à¸šà¸£à¸´à¸«à¸²à¸£à¹€à¸¡à¸·à¸­à¸‡"));
        _cityPriority = AddEnumOption<CityPriority>(root, PriorityThai);
        _cityBorder = AddEnumOption<BorderPolicy>(root, BorderThai);
        _cityTax = AddSliderRow(root, "à¸ à¸²à¸©à¸µ", 0, 0.5, 0.01, 0.12);
        _cityBirth = AddSliderRow(root, "à¸™à¹‚à¸¢à¸šà¸²à¸¢à¸à¸²à¸£à¹€à¸à¸´à¸”", 0, 2, 0.05, 1);
        _cityFoodReserve = AddSpinRow(root, "à¸­à¸²à¸«à¸²à¸£à¸ªà¸³à¸£à¸­à¸‡à¹€à¸›à¹‰à¸²à¸«à¸¡à¸²à¸¢", 0, 2000, 10, 120);
        _cityPopulationLimit = AddSpinRow(root, "à¹€à¸à¸”à¸²à¸™à¸›à¸£à¸°à¸Šà¸²à¸à¸£à¹€à¸¡à¸·à¸­à¸‡", 0, 3000, 10, 500);
        _cityAutoBuild = new CheckButton { Text = "à¸à¹ˆà¸­à¸ªà¸£à¹‰à¸²à¸‡à¸­à¸±à¸•à¹‚à¸™à¸¡à¸±à¸•à¸´", ButtonPressed = true };
        _cityQuarantine = new CheckButton { Text = "à¸à¸±à¸à¸à¸±à¸™à¹‚à¸£à¸„" };
        _cityEvacuate = new CheckButton { Text = "à¸­à¸à¸¢à¸à¸›à¸£à¸°à¸Šà¸²à¸à¸£" };
        root.AddChild(_cityAutoBuild);
        root.AddChild(_cityQuarantine);
        root.AddChild(_cityEvacuate);
        foreach (Control control in new Control[] { _cityPriority, _cityBorder, _cityTax, _cityBirth, _cityFoodReserve, _cityPopulationLimit, _cityAutoBuild, _cityQuarantine, _cityEvacuate })
        {
            if (control is OptionButton option) option.ItemSelected += _ => ApplySelectedCityPolicy();
            else if (control is Godot.Range range) range.ValueChanged += _ => ApplySelectedCityPolicy();
            else if (control is BaseButton button) button.Toggled += _ => ApplySelectedCityPolicy();
        }
        var cityActions = new HBoxContainer();
        cityActions.AddChild(CreateButton("à¸ªà¸£à¹‰à¸²à¸‡à¸­à¸²à¸„à¸²à¸£", BuildSelectedCity));
        cityActions.AddChild(CreateButton("à¹€à¸—à¸¨à¸à¸²à¸¥", FestivalSelectedCity));
        cityActions.AddChild(CreateButton("à¸£à¸±à¸à¸©à¸²à¹‚à¸£à¸„", HealSelectedCity));
        root.AddChild(cityActions);

        root.AddChild(new HSeparator());
        root.AddChild(Section("à¸šà¸£à¸´à¸«à¸²à¸£à¸­à¸²à¸“à¸²à¸ˆà¸±à¸à¸£"));
        _kingdomBorder = AddEnumOption<BorderPolicy>(root, BorderThai);
        _kingdomTax = AddSliderRow(root, "à¸•à¸±à¸§à¸„à¸¹à¸“à¸ à¸²à¸©à¸µ", 0.5, 2, 0.05, 1);
        _kingdomBirth = AddSliderRow(root, "à¸™à¹‚à¸¢à¸šà¸²à¸¢à¸à¸²à¸£à¹€à¸à¸´à¸”", 0, 2, 0.05, 1);
        _kingdomMilitary = AddSliderRow(root, "à¸„à¸§à¸²à¸¡à¸ªà¸³à¸„à¸±à¸à¸à¸­à¸‡à¸—à¸±à¸", 0, 1, 0.05, 0.5);
        _kingdomPopulationLimit = AddSpinRow(root, "à¹€à¸à¸”à¸²à¸™à¸›à¸£à¸°à¸Šà¸²à¸à¸£à¸­à¸²à¸“à¸²à¸ˆà¸±à¸à¸£", 0, 6000, 25, 2500);
        _kingdomPreferPeace = new CheckButton { Text = "à¹ƒà¸«à¹‰à¸„à¸§à¸²à¸¡à¸ªà¸³à¸„à¸±à¸à¸à¸±à¸šà¸ªà¸±à¸™à¸•à¸´", ButtonPressed = true };
        root.AddChild(_kingdomPreferPeace);
        foreach (Control control in new Control[] { _kingdomBorder, _kingdomTax, _kingdomBirth, _kingdomMilitary, _kingdomPopulationLimit, _kingdomPreferPeace })
        {
            if (control is OptionButton option) option.ItemSelected += _ => ApplySelectedKingdomPolicy();
            else if (control is Godot.Range range) range.ValueChanged += _ => ApplySelectedKingdomPolicy();
            else if (control is BaseButton button) button.Toggled += _ => ApplySelectedKingdomPolicy();
        }

        root.AddChild(new HSeparator());
        root.AddChild(Section("Scenario à¹à¸¥à¸°à¹€à¸›à¹‰à¸²à¸«à¸¡à¸²à¸¢"));
        _scenarioSelector = new OptionButton();
        foreach (ScenarioKind scenario in Enum.GetValues<ScenarioKind>())
            _scenarioSelector.AddItem(ScenarioThai(scenario), (int)scenario);
        _scenarioSelector.ItemSelected += id =>
        {
            _director?.SelectScenario((ScenarioKind)(int)id);
            UpdateScenario();
        };
        root.AddChild(_scenarioSelector"“°¢÷66Væ&–ôÆ&VÂÒæWrÆ&VÂ²WF÷w&ÖöFRÒFW‡E6W'fW"äWF÷w&ÖöFRåv÷&E6Ö'BÓ°¢&ö÷BäFD6†–ÆB…÷66Væ&–ôÆ&VÂ“°¢÷66Væ&–õ&öw&W72ÒæWr&öw&W74&"²Ö–åfÇVRÒÂÖ…fÇVRÒÂfÇVRÒÂ6†÷uW&6VçFvRÒG'VRÓ°¢&ö÷BäFD6†–ÆB…÷66Væ&–õ&öw&W72“° ¢öWfVçEæVÂÒæWræVÄ6öçF–æW"‚“°¢&ö÷BäFD6†–ÆB…öWfVçEæVÂ“°¢f"WfVçE&ö÷BÒæWrd&÷„6öçF–æW"‚“°¢öWfVçEæVÂäFD6†–ÆB†WfVçE&ö÷B“°¢öWfVçEF—FÆRÒæWrÆ&VÂ²FW‡BÒ.˜Š¾‰^‹ˆ‹.Š>‰>˜Â"Ó°¢öWfVçDFW67&—F–öâÒæWrÆ&VÂ²WF÷w&ÖöFRÒFW‡E6W'fW"äWF÷w&ÖöFRåv÷&E6Ö'BÓ°¢WfVçE&ö÷BäFD6†–ÆB…öWfVçEF—FÆR“°¢WfVçE&ö÷BäFD6†–ÆB…öWfVçDFW67&—F–öâ“°¢f÷"†–çB’Ò²’ÂöWfVçD6†ö–6T'WGFöç2äÆVæwFƒ²’²²¢°¢–çB6†ö–6RÒ“°¢öWfVçD6†ö–6T'WGFöç5¶•ÒÒ7&VFT'WGFöâ‚B.‰~‹.ˆ~˜Š^‹~ŠŞˆ¶’²Ò"Â‚’Óâ&W6öÇfTWfVçB†6†ö–6R’“°¢WfVçE&ö÷BäFD6†–ÆB…öWfVçD6†ö–6T'WGFöç5¶•Ò“°¢Ğ¢öWfVçEæVÂåf—6–&ÆRÒfÇ6S° ¢&ö÷BäFD6†–ÆB†æWr…6W&F÷"‚’“°¢&ö÷BäFD6†–ÆB…6V7F–öâ‚$6‡&öæ–6ÆR"’“°¢f"f–ÇFW%&÷rÒæWr„&÷„6öçF–æW"‚“°¢ö6‡&öæ–6ÆTf–ÇFW"ÒæWr÷F–öä'WGFöâ‚“°¢f÷&V6‚„6‡&öæ–6ÆTf–ÇFW"f–ÇFW"–âVçVÒävWEfÇVW3Ä6‡&öæ–6ÆTf–ÇFW#â‚’¢ö6‡&öæ–6ÆTf–ÇFW"äFD—FVÒ„f–ÇFW%F†’†f–ÇFW"’Â†–çB–f–ÇFW"“°¢ö6‡&öæ–6ÆTf–ÇFW"ä—FVÕ6VÆV7FVB³ÒòÓâ&V'V–ÆD6‡&öæ–6ÆR‚“°¢f–ÇFW%&÷räFD6†–ÆB…ö6‡&öæ–6ÆTf–ÇFW"“°¢ö6‡&öæ–6ÆU6V&6‚ÒæWrÆ–æTVF—B²Æ6V†öÆFW%FW‡BÒ.ˆŠ>ŠŞˆr˜Š¾‰^‹ˆ‹.Š>‰>˜Â"Â6—¦TfÆw4†÷&—¦öçFÂÒ6öçG&öÂå6—¦TfÆw2äW‡æDf–ÆÂÓ°¢ö6‡&öæ–6ÆU6V&6‚åFW‡D6†ævVB³ÒòÓâ&V'V–ÆD6‡&öæ–6ÆR‚“°¢f–ÇFW%&÷räFD6†–ÆB…ö6‡&öæ–6ÆU6V&6‚“°¢&ö÷BäFD6†–ÆB†f–ÇFW%&÷r“°¢ö6‡&öæ–6ÆTÆ—7BÒæWr—FVÔÆ—7B²7W7FöÔÖ–æ–×VÕ6—¦RÒæWrfV7F÷#"ƒ3cÂ#3’Â6VÆV7DÖöFRÒ—FVÔÆ—7Bå6VÆV7DÖöFTVçVÒå6–ævÆRÓ°¢ö6‡&öæ–6ÆTÆ—7Bä—FVÕ6VÆV7FVB³Ò–æFW‚Óâfö7W46‡&öæ–6ÆR‚†–çB––æFW‚“°¢&ö÷BäFD6†–ÆB…ö6‡&öæ–6ÆTÆ—7B“°¢Ğ §Ğ