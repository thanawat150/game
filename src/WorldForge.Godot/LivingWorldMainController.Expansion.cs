using System.Text.Json;
using Godot;
using WorldForge.Core.Simulation;
using WorldForge.Presentation;

namespace WorldForge;

public sealed partial class LivingWorldMainController : Node2D
{
    private CanvasLayer _expansionLayer = null!;
    private PanelContainer _expansionPanel = null!;
    private Label _expansionSummary = null!;
    private Label _campaignExpansionLabel = null!;
    private ProgressBar _campaignExpansionProgress = null!;
    private Label _faithLabel = null!;
    private OptionButton _deityPathOption = null!;
    private OptionButton _miracleOption = null!;
    private OptionButton _buildingOption = null!;
    private OptionButton _fleetMissionOption = null!;
    private OptionButton _spellOption = null!;
    private ItemList _legendList = null!;
    private ItemList _achievementList = null!;
    private CheckButton _replayEnabled = null!;
    private HSlider _replaySlider = null!;
    private Label _replayLabel = null!;
    private CheckButton _showExpansionRoads = null!;
    private CheckButton _showExpansionFaith = null!;
    private CheckButton _showExpansionMagic = null!;
    private Label _shareStatus = null!;
    private int _lastExpansionUiDay = -1;
    private int _lastExpansionLegendCount = -1;
    private int _selectedLegendIndex;
    private readonly List<ulong> _visibleLegendIds = new();

    private void BuildExpansionInterface()
    {
        _expansionLayer = new CanvasLayer { Name = "ExpansionInterface", Layer = 18 };
        AddChild(_expansionLayer);
        _expansionPanel = new PanelContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = -620,
            OffsetRight = -8,
            OffsetTop = 64,
            OffsetBottom = -36,
            Visible = false,
        };
        _expansionLayer.AddChild(_expansionPanel);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _expansionPanel.AddChild(scroll);
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(580, 0) };
        root.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(root);

        var titleRow = new HBoxContainer();
        titleRow.AddChild(new Label { Text = "LEGENDS • CITIES • FAITH • HISTORY", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        titleRow.AddChild(CreateButton("ปิด [F10]", ToggleExpansionPanel));
        root.AddChild(titleRow);
        _expansionSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_expansionSummary);

        root.AddChild(new HSeparator());
        root.AddChild(Section("Campaign และความสำเร็จ"));
        _campaignExpansionLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_campaignExpansionLabel);
        _campaignExpansionProgress = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = true };
        root.AddChild(_campaignExpansionProgress);
        _achievementList = new ItemList { CustomMinimumSize = new Vector2(0, 120), SelectMode = ItemList.SelectModeEnum.Single };
        root.AddChild(_achievementList);

        root.AddChild(new HSeparator());
        root.AddChild(Section("ศรัทธา เส้นทางเทพ และปาฏิหาริย์"));
        _faithLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_faithLabel);
        _deityPathOption = AddEnumOption<DeityPath>(root, DeityPathThai);
        _deityPathOption.ItemSelected += id =>
        {
            if (_expansion is null) return;
            _expansion.SetDeityPath((DeityPath)_deityPathOption.GetItemId((int)id));
            _renderDirty = true;
        };
        _miracleOption = AddEnumOption<MiracleKind>(root, MiracleThai);
        root.AddChild(CreateButton("ใช้ปาฏิหาริย์กับเมืองที่เลือก", UseSelectedMiracle));

        root.AddChild(new HSeparator());
        root.AddChild(Section("เมืองจริงและสายการผลิต"));
        _buildingOption = AddEnumOption<BuildingKind>(root, BuildingThai);
        var cityButtons = new HBoxContainer();
        cityButtons.AddChild(CreateButton("วางผังอาคาร", PlanSelectedBuilding));
        cityButtons.AddChild(CreateButton("ซ่อมเมือง", RepairSelectedCity));
        cityButtons.AddChild(CreateButton("สร้างอนุสาวรีย์", CommissionSelectedLegendMonument));
        root.AddChild(cityButtons);

        root.AddChild(new HSeparator());
        root.AddChild(Section("กองเรือ เวทมนตร์ และการสำรวจ"));
        _fleetMissionOption = AddEnumOption<FleetMission>(root, mission => mission switch
        {
            FleetMission.Trade => "ค้าขาย",
            FleetMission.Explore => "สำรวจ",
            FleetMission.Patrol => "ลาดตระเวน",
            FleetMission.Raid => "ปล้นชายฝั่ง",
            FleetMission.Invade => "รุกราน",
            FleetMission.Return => "กลับท่า",
            _ => "ว่าง",
        });
        root.AddChild(CreateButton("สร้างกองเรือจากเมืองที่เลือก", CreateSelectedFleet));
        _spellOption = AddEnumOption<SpellKind>(root, SpellThai);
        root.AddChild(CreateButton("ให้นักเวทที่เลือกใช้เวท", CastSelectedSpell));

        root.AddChild(new HSeparator());
        root.AddChild(Section("บุคคลสำคัญและความทรงจำ"));
        _legendList = new ItemList { CustomMinimumSize = new Vector2(0, 170), SelectMode = ItemList.SelectModeEnum.Single };
        _legendList.ItemSelected += index => _selectedLegendIndex = (int)index;
        root.AddChild(_legendList);
        var legendButtons = new HBoxContainer();
        legendButtons.AddChild(CreateButton("ไปหาตำนาน", FocusSelectedLegend));
        legendButtons.AddChild(CreateButton("อนุสาวรีย์ในเมืองที่เลือก", CommissionSelectedLegendMonument));
        root.AddChild(legendButtons);

        root.AddChild(new HSeparator());
        root.AddChild(Section("ประวัติศาสตร์และ Replay"));
        _replayEnabled = new CheckButton { Text = "เปิดโหมดดูอดีต" };
        _replayEnabled.Toggled += enabled => UpdateReplayRenderer();
        root.AddChild(_replayEnabled);
        _replaySlider = new HSlider { MinValue = 0, MaxValue = 0, Step = 1, Value = 0, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _replaySlider.ValueChanged += _ => UpdateReplayRenderer();
        root.AddChild(_replaySlider);
        _replayLabel = new Label();
        root.AddChild(_replayLabel);
        var exportRow = new HBoxContainer();
        exportRow.AddChild(CreateButton("Export พงศาวดาร", ExportHistoryReport));
        exportRow.AddChild(CreateButton("Export โลกสำหรับแชร์", ExportSharePackage));
        exportRow.AddChild(CreateButton("Import โลกที่แชร์", ImportSharePackage));
        root.AddChild(exportRow);

        root.AddChild(new HSeparator());
        root.AddChild(Section("ภาพและ Mod Rules"));
        _showExpansionRoads = new CheckButton { Text = "แสดงถนนและอาคารจริง", ButtonPressed = true };
        _showExpansionFaith = new CheckButton { Text = "แสดงออร่าศรัทธา", ButtonPressed = true };
        _showExpansionMagic = new CheckButton { Text = "แสดงเอฟเฟกต์เวท", ButtonPressed = true };
        _showExpansionRoads.Toggled += value => { _expansionRenderer.ShowRoads = value; _renderDirty = true; };
        _showExpansionFaith.Toggled += value => { _expansionRenderer.ShowFaith = value; _renderDirty = true; };
        _showExpansionMagic.Toggled += value => { _expansionRenderer.ShowMagic = value; _renderDirty = true; };
        root.AddChild(_showExpansionRoads);
        root.AddChild(_showExpansionFaith);
        root.AddChild(_showExpansionMagic);
        var modRow = new HBoxContainer();
        modRow.AddChild(CreateButton("Export Mod Template", ExportModPack));
        modRow.AddChild(CreateButton("Import Mod Rules", ImportModPack));
        root.AddChild(modRow);
        _shareStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_shareStatus);
    }

    private void EnsureExpansionRuntime()
    {
        if (_world is null || _simulation is null || _director is null) return;
        if (_expansion is null || !ReferenceEquals(_expansionBoundSimulation, _simulation))
        {
            _expansion = new WorldExpansionDirector(_world, _simulation, _director, _world.Config.Seed ^ 0x4C4547454E44L);
            _expansionBoundSimulation = _simulation;
            _expansionRenderer.Bind(_world, _simulation, _director, _expansion);
            _lastExpansionUiDay = -1;
            _lastExpansionLegendCount = -1;
        }
    }

    private void UpdateExpansionCamera()
    {
        _expansionRenderer.CameraPosition = _camera.Position;
        _expansionRenderer.CameraZoom = _camera.Zoom;
        _expansionRenderer.ViewportSize = GetViewportRect().Size;
    }

    private void ToggleExpansionPanel()
    {
        _expansionPanel.Visible = !_expansionPanel.Visible;
        if (_expansionPanel.Visible) RefreshExpansionUi(force: true);
    }

    private void RefreshExpansionUi(bool force = false)
    {
        if (_expansion is null || _simulation is null) return;
        WorldExpansionState state = _expansion.State;
        _expansionSummary.Text = $"ตำนาน {state.Legends.Count:N0} | อาคารจริง {state.CityDistricts.Values.Sum(d => d.Buildings.Count):N0} | กองเรือ {state.Fleets.Values.Count(f => f.IsActive)} | ชนเผ่าเร่ร่อน {state.Nomads.Values.Count(n => n.Active)} | นักเวท {state.Mages.Count} | ซากที่สำรวจ {state.Ruins.Values.Count(r => r.Explored)}/{state.Ruins.Count}";
        _campaignExpansionLabel.Text = $"{state.Campaign.Title}\n{state.Campaign.Objective}";
        _campaignExpansionProgress.Value = state.Campaign.Progress * 100;
        _faithLabel.Text = $"เส้นทาง {DeityPathThai(state.Faith.Path)} | ศรัทธา {state.Faith.Faith:0.0} | ความหวาดกลัว {state.Faith.Fear:0.0} | พลังศักดิ์สิทธิ์ {state.Faith.Favor:0.0}/{state.Faith.MaxFavor:0.0}\nปาฏิหาริย์ที่ปลดล็อก: {string.Join(", ", state.Faith.UnlockedMiracles.OrderBy(m => m).Select(MiracleThai))}";
        SelectOptionById(_deityPathOption, (int)state.Faith.Path);
        _replaySlider.MaxValue = Math.Max(0, state.History.Count - 1);
        if (!_replayEnabled.ButtonPressed) _replaySlider.Value = _replaySlider.MaxValue;
        UpdateReplayLabel();

        if (!force && _lastExpansionUiDay == _simulation.State.Day && _lastExpansionLegendCount == state.Legends.Count) return;
        _lastExpansionUiDay = _simulation.State.Day;
        _lastExpansionLegendCount = state.Legends.Count;
        RebuildLegendList();
        RebuildAchievementList();
    }

    private void RebuildLegendList()
    {
        if (_expansion is null) return;
        _legendList.Clear();
        _visibleLegendIds.Clear();
        foreach (LegendProfile legend in _expansion.State.Legends.Values.OrderByDescending(l => l.Fame + l.Legacy).ThenBy(l => l.EntityId).Take(120))
        {
            _visibleLegendIds.Add(legend.EntityId);
            string status = legend.IsDead ? "†" : "●";
            string memories = legend.Memories.Count == 0 ? string.Empty : $" | {legend.Memories[^1].Summary}";
            _legendList.AddItem($"{status} {_expansion.DisplayLegendName(legend)} | Fame {legend.Fame} Legacy {legend.Legacy}{memories}");
        }
        _selectedLegendIndex = Math.Clamp(_selectedLegendIndex, 0, Math.Max(0, _visibleLegendIds.Count - 1));
        if (_visibleLegendIds.Count > 0) _legendList.Select(_selectedLegendIndex);
    }

    private void RebuildAchievementList()
    {
        if (_expansion is null) return;
        _achievementList.Clear();
        foreach (AchievementState achievement in _expansion.State.Achievements.Values.OrderByDescending(a => a.Unlocked).ThenBy(a => a.Title))
        {
            string state = achievement.Unlocked ? "✓" : "○";
            _achievementList.AddItem($"{state} {achievement.Title} — {achievement.Progress:0}/{achievement.Target:0} | {achievement.Description}");
        }
    }

    private ulong? SelectedSettlementId => _livingRenderer.SelectedSettlementId;
    private ulong? SelectedEntityId => _livingRenderer.SelectedEntityId;

    private void UseSelectedMiracle()
    {
        if (_expansion is null) return;
        MiracleKind miracle = (MiracleKind)_miracleOption.GetSelectedId();
        bool success = _expansion.UseMiracle(miracle, SelectedSettlementId);
        _shareStatus.Text = success ? $"ใช้ {MiracleThai(miracle)} สำเร็จ" : "ใช้ปาฏิหาริย์ไม่ได้: ยังไม่ปลดล็อก พลังไม่พอ หรือไม่ได้เลือกเมือง";
        _renderDirty = true;
    }

    private void PlanSelectedBuilding()
    {
        if (_expansion is null || SelectedSettlementId is not ulong cityId) { _shareStatus.Text = "เลือกเมืองก่อน"; return; }
        BuildingKind kind = (BuildingKind)_buildingOption.GetSelectedId();
        _shareStatus.Text = _expansion.PlanBuilding(cityId, kind) ? $"เพิ่ม {BuildingThai(kind)} เข้าคิวก่อสร้าง" : "วางผังไม่ได้หรือคิวก่อสร้างเต็ม";
        _renderDirty = true;
    }

    private void RepairSelectedCity()
    {
        if (_expansion is null || SelectedSettlementId is not ulong cityId) { _shareStatus.Text = "เลือกเมืองก่อน"; return; }
        _shareStatus.Text = _expansion.RepairCity(cityId) ? "เริ่มซ่อมอาคารที่เสียหาย" : "ไม่มีอาคารเสียหายหรือทรัพยากรไม่พอ";
        _renderDirty = true;
    }

    private void CreateSelectedFleet()
    {
        if (_expansion is null || SelectedSettlementId is not ulong cityId) { _shareStatus.Text = "เลือกเมืองชายฝั่งก่อน"; return; }
        FleetMission mission = (FleetMission)_fleetMissionOption.GetSelectedId();
        FleetState? fleet = _expansion.CreateFleet(cityId, mission);
        _shareStatus.Text = fleet is null ? "สร้างกองเรือไม่ได้: ต้องมีท่าเรือ ไม้ ทอง และทะเลเชื่อมต่อ" : $"สร้าง {fleet.Name} แล้ว";
        _renderDirty = true;
    }

    private void CastSelectedSpell()
    {
        if (_expansion is null || SelectedEntityId is not ulong mageId || _simulation is null) { _shareStatus.Text = "เลือกนักเวทก่อน"; return; }
        SimEntity? mage = _simulation.State.Entities.GetValueOrDefault(mageId);
        if (mage is null) return;
        SpellKind spell = (SpellKind)_spellOption.GetSelectedId();
        _shareStatus.Text = _expansion.CastSpell(mageId, spell, mage.X, mage.Y) ? $"ใช้เวท {SpellThai(spell)}" : "ใช้เวทไม่ได้: ไม่รู้เวทนี้หรือ Mana ไม่พอ";
        _renderDirty = true;
    }

    private void FocusSelectedLegend()
    {
        if (_simulation is null || _visibleLegendIds.Count == 0) return;
        ulong id = _visibleLegendIds[Math.Clamp(_selectedLegendIndex, 0, _visibleLegendIds.Count - 1)];
        SimEntity? entity = _simulation.State.Entities.GetValueOrDefault(id);
        if (entity is null) { _shareStatus.Text = "ตำนานนี้เสียชีวิตแล้ว สามารถอ่านได้จากพงศาวดาร"; return; }
        _camera.Position = TileCenter(entity.X, entity.Y);
        _camera.Zoom = new Vector2(Math.Max(1.3f, _camera.Zoom.X), Math.Max(1.3f, _camera.Zoom.Y));
        _livingRenderer.SelectEntity(id);
        _renderDirty = true;
    }

    private void CommissionSelectedLegendMonument()
    {
        if (_expansion is null || SelectedSettlementId is not ulong cityId || _visibleLegendIds.Count == 0) { _shareStatus.Text = "เลือกเมืองและตำนานก่อน"; return; }
        ulong legendId = _visibleLegendIds[Math.Clamp(_selectedLegendIndex, 0, _visibleLegendIds.Count - 1)];
        _shareStatus.Text = _expansion.CommissionMonument(cityId, legendId) ? "เริ่มสร้างอนุสาวรีย์แล้ว" : "สร้างไม่ได้: หินหรือทองไม่พอ";
        _renderDirty = true;
    }

    private void UpdateReplayRenderer()
    {
        if (_expansion is null) return;
        _expansionRenderer.ReplaySnapshotIndex = _replayEnabled.ButtonPressed ? (int)_replaySlider.Value : -1;
        UpdateReplayLabel();
        _renderDirty = true;
    }

    private void UpdateReplayLabel()
    {
        if (_expansion is null || _expansion.State.History.Count == 0) { _replayLabel.Text = "ยังไม่มีข้อมูลประวัติศาสตร์"; return; }
        WorldHistorySnapshot snapshot = _expansion.State.History[Math.Clamp((int)_replaySlider.Value, 0, _expansion.State.History.Count - 1)];
        _replayLabel.Text = $"ปี {snapshot.Year} เดือน {snapshot.Month} | ประชากร {snapshot.Population:N0} | เมือง {snapshot.Settlements} | อาณาจักร {snapshot.Kingdoms} | กองเรือ {snapshot.Fleets}";
    }

    private void ExportHistoryReport()
    {
        if (_expansion is null) return;
        string path = ProjectSettings.GlobalizePath("user://exports/world_history.txt");
        WriteAtomic(path, _expansion.GenerateHistoryReport());
        _shareStatus.Text = $"Export พงศาวดารแล้ว: {path}";
    }

    private sealed class SharedWorldPackage
    {
        public int Version { get; set; } = 1;
        public string World { get; set; } = string.Empty;
        public string Simulation { get; set; } = string.Empty;
        public string Living { get; set; } = string.Empty;
        public string Expansion { get; set; } = string.Empty;
        public string Meta { get; set; } = string.Empty;
    }

    private void ExportSharePackage()
    {
        if (_world is null || _simulation is null || _director is null || _expansion is null) return;
        SaveWorld(showStatus: false);
        var package = new SharedWorldPackage
        {
            World = File.ReadAllText(WorldSavePath(CurrentSlot)),
            Simulation = File.ReadAllText(SimulationSavePath(CurrentSlot)),
            Living = File.ReadAllText(LivingSavePath(CurrentSlot)),
            Expansion = File.ReadAllText(ExpansionSavePath(CurrentSlot)),
            Meta = File.Exists(MetaSavePath(CurrentSlot)) ? File.ReadAllText(MetaSavePath(CurrentSlot)) : string.Empty,
        };
        string path = ProjectSettings.GlobalizePath("user://exports/shared_world.worldforge.json");
        WriteAtomic(path, JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true }));
        _shareStatus.Text = $"Export โลกสำหรับส่งให้ผู้อื่นแล้ว: {path}\nนี่คือการแชร์ไฟล์โลก ไม่ใช่ Multiplayer ออนไลน์แบบ Real-time";
    }

    private void ImportSharePackage()
    {
        string path = ProjectSettings.GlobalizePath("user://exports/shared_world.worldforge.json");
        if (!File.Exists(path)) { _shareStatus.Text = $"ไม่พบไฟล์ {path}"; return; }
        SharedWorldPackage? package = JsonSerializer.Deserialize<SharedWorldPackage>(File.ReadAllText(path));
        if (package is null || string.IsNullOrWhiteSpace(package.World)) { _shareStatus.Text = "ไฟล์โลกไม่ถูกต้อง"; return; }
        WriteAtomic(WorldSavePath(CurrentSlot), package.World);
        WriteAtomic(SimulationSavePath(CurrentSlot), package.Simulation);
        WriteAtomic(LivingSavePath(CurrentSlot), package.Living);
        WriteAtomic(ExpansionSavePath(CurrentSlot), package.Expansion);
        if (!string.IsNullOrWhiteSpace(package.Meta)) WriteAtomic(MetaSavePath(CurrentSlot), package.Meta);
        LoadWorld();
        _shareStatus.Text = $"Import โลกเข้า Save ช่อง {CurrentSlot} แล้ว";
    }

    private void ExportModPack()
    {
        if (_expansion is null) return;
        string path = ProjectSettings.GlobalizePath("user://mods/worldforge_expansion_mod.json");
        WriteAtomic(path, _expansion.ExportModPack());
        _shareStatus.Text = $"Export Mod Template แล้ว: {path}";
    }

    private void ImportModPack()
    {
        if (_expansion is null) return;
        string path = ProjectSettings.GlobalizePath("user://mods/worldforge_expansion_mod.json");
        if (!File.Exists(path)) { _shareStatus.Text = "ยังไม่มีไฟล์ Mod ให้ Import กด Export Mod Template ก่อน"; return; }
        try
        {
            _expansion.ImportModPack(File.ReadAllText(path));
            _shareStatus.Text = "Import Mod Rules สำเร็จ";
        }
        catch (Exception exception)
        {
            _shareStatus.Text = $"Import Mod ไม่สำเร็จ: {exception.Message}";
        }
    }

    private static string DeityPathThai(DeityPath path) => path switch
    {
        DeityPath.Mercy => "เทพแห่งเมตตา",
        DeityPath.Nature => "เทพแห่งธรรมชาติ",
        DeityPath.War => "เทพแห่งสงคราม",
        DeityPath.Knowledge => "เทพแห่งความรู้",
        _ => "เทพแห่งความหวาดกลัว",
    };

    private static string MiracleThai(MiracleKind miracle) => miracle switch
    {
        MiracleKind.BlessHarvest => "พรแห่งพืชผล",
        MiracleKind.HealCity => "รักษาทั้งนคร",
        MiracleKind.Inspire => "ปลุกขวัญประชาชน",
        MiracleKind.Smite => "ทัณฑ์สวรรค์",
        MiracleKind.RaiseForest => "กำเนิดผืนป่า",
        MiracleKind.RevealRuins => "เปิดเผยซากโบราณ",
        MiracleKind.CalmSea => "สงบมหาสมุทร",
        _ => miracle.ToString(),
    };

    private static string BuildingThai(BuildingKind kind) => kind switch
    {
        BuildingKind.House => "บ้าน",
        BuildingKind.Farm => "ฟาร์ม",
        BuildingKind.Lumberyard => "ลานไม้",
        BuildingKind.Quarry => "เหมืองหิน",
        BuildingKind.Mine => "เหมืองแร่",
        BuildingKind.Sawmill => "โรงเลื่อย",
        BuildingKind.Smelter => "โรงหลอม",
        BuildingKind.Workshop => "โรงช่าง",
        BuildingKind.Market => "ตลาด",
        BuildingKind.Temple => "วิหาร",
        BuildingKind.Clinic => "สถานพยาบาล",
        BuildingKind.Barracks => "ค่ายทหาร",
        BuildingKind.Watchtower => "หอสังเกตการณ์",
        BuildingKind.Wall => "กำแพง",
        BuildingKind.Gate => "ประตูเมือง",
        BuildingKind.Keep => "ปราสาท",
        BuildingKind.Harbor => "ท่าเรือ",
        BuildingKind.Shipyard => "อู่ต่อเรือ",
        BuildingKind.MageTower => "หอคอยเวท",
        BuildingKind.Monument => "อนุสาวรีย์",
        _ => kind.ToString(),
    };

    private static string SpellThai(SpellKind spell) => spell switch
    {
        SpellKind.Growth => "เร่งการเติบโต",
        SpellKind.Fireball => "ลูกไฟ",
        SpellKind.Heal => "รักษา",
        SpellKind.StormCall => "เรียกพายุ",
        SpellKind.Teleport => "เคลื่อนย้าย",
        SpellKind.Ward => "เกราะคุ้มครอง",
        SpellKind.AnimateRuins => "ปลุกผู้พิทักษ์ซาก",
        _ => spell.ToString(),
    };

    private static void SelectOptionById(OptionButton option, int id)
    {
        for (int i = 0; i < option.ItemCount; i++)
            if (option.GetItemId(i) == id) { option.Select(i); return; }
    }
}
