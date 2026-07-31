namespace WorldForge.Core.Simulation;

public sealed partial class WorldExpansionDirector
{
    private void InitializeAchievements()
    {
        AddAchievement("first_legend", "ตำนานคนแรก", "มีบุคคลสำคัญคนแรกในโลก", 1);
        AddAchievement("great_legend", "ชื่อที่โลกจดจำ", "สร้างตำนานที่มีชื่อเสียงอย่างน้อย 200", 200);
        AddAchievement("city_builder", "นครที่สร้างด้วยมือ", "มีสิ่งปลูกสร้างจริง 25 แห่ง", 25);
        AddAchievement("production_age", "ยุคอุตสาหกรรม", "ผลิตเครื่องมือและอาวุธรวม 50 หน่วย", 50);
        AddAchievement("faithful_world", "โลกแห่งศรัทธา", "สะสมศรัทธา 250", 250);
        AddAchievement("age_of_sails", "ยุคแห่งการเดินเรือ", "มีกองเรือทำงานพร้อมกัน 3 กอง", 3);
        AddAchievement("ruin_seeker", "ผู้เปิดเผยอดีต", "สำรวจซากโบราณ 5 แห่ง", 5);
        AddAchievement("arcane_age", "ยุคอาคม", "มีนักเวทระดับ 5", 5);
        AddAchievement("nomad_nation", "จากผู้พเนจรสู่อาณาจักร", "มีชนเผ่าเร่ร่อนตั้งถิ่นฐาน", 1);
        AddAchievement("century_chronicle", "ศตวรรษแห่งเรื่องเล่า", "บันทึกประวัติศาสตร์ครบ 100 ปี", 100);
        AddAchievement("world_shaper", "ผู้กำหนดชะตา", "ใช้ปาฏิหาริย์ครบ 10 ครั้ง", 10);
    }

    private void AddAchievement(string id, string title, string description, float target)
    {
        if (State.Achievements.ContainsKey(id)) return;
        State.Achievements[id] = new AchievementState
        {
            Id = id,
            Title = title,
            Description = description,
            Target = target,
        };
    }

    private void UpdateAchievementsAndCampaign()
    {
        SetAchievementProgress("first_legend", State.Legends.Count);
        SetAchievementProgress("great_legend", State.Legends.Values.DefaultIfEmpty().Max(l => l?.Fame ?? 0));
        SetAchievementProgress("city_builder", State.CityDistricts.Values.Sum(d => d.Buildings.Count(b => b.Status == BuildingStatus.Active)));
        float manufactured = State.CityDistricts.Values.Sum(d => d.Stockpile.GetValueOrDefault(ResourceKind.Tools) + d.Stockpile.GetValueOrDefault(ResourceKind.Weapons));
        SetAchievementProgress("production_age", manufactured);
        SetAchievementProgress("faithful_world", State.Faith.Faith);
        SetAchievementProgress("age_of_sails", State.Fleets.Values.Count(f => f.IsActive));
        SetAchievementProgress("ruin_seeker", State.Ruins.Values.Count(r => r.Explored));
        SetAchievementProgress("arcane_age", State.Mages.Values.DefaultIfEmpty().Max(m => m?.Level ?? 0));
        SetAchievementProgress("nomad_nation", State.Nomads.Values.Count(n => !n.Active && n.State == NomadStateKind.Settling));
        SetAchievementProgress("century_chronicle", _simulation.State.Year);
        SetAchievementProgress("world_shaper", _simulation.State.Chronicle.Count(c => c.Type == "faith.miracle"));
        UpdateCampaign();
    }

    private void SetAchievementProgress(string id, float progress)
    {
        if (!State.Achievements.TryGetValue(id, out AchievementState? achievement)) return;
        achievement.Progress = Math.Max(achievement.Progress, progress);
        if (achievement.Unlocked || achievement.Progress < achievement.Target) return;
        achievement.Unlocked = true;
        achievement.UnlockedDay = _simulation.State.Day;
        AddChronicle("achievement.unlocked", "ปลดล็อกความสำเร็จ", achievement.Title, _world.Width / 2, _world.Height / 2, 2);
    }

    private void UpdateCampaign()
    {
        CampaignProgressState campaign = State.Campaign;
        switch (campaign.Chapter)
        {
            case CampaignChapter.Awakening:
                campaign.Title = "เสียงเรียกแห่งโลก";
                campaign.Objective = "รักษาเมืองและประชากรอย่างน้อย 20 คน";
                campaign.Progress = Math.Clamp(_simulation.State.Entities.Values.Count(e => e.IsAlive && e.Species == SpeciesKind.Settler) / 20f, 0, 1);
                if (campaign.Progress >= 1) AdvanceCampaign(CampaignChapter.FirstLegend);
                break;
            case CampaignChapter.FirstLegend:
                campaign.Title = "ผู้ที่โลกจะจดจำ";
                campaign.Objective = "สร้างตำนานที่มีชื่อเสียง 80";
                campaign.Progress = Math.Clamp(State.Legends.Values.DefaultIfEmpty().Max(l => l?.Fame ?? 0) / 80f, 0, 1);
                if (campaign.Progress >= 1) AdvanceCampaign(CampaignChapter.SacredCity);
                break;
            case CampaignChapter.SacredCity:
                campaign.Title = "นครศักดิ์สิทธิ์";
                campaign.Objective = "สร้างวิหารและสะสมศรัทธา 100";
                bool temple = State.CityDistricts.Values.Any(d => d.Buildings.Any(b => b.Kind == BuildingKind.Temple && b.Status == BuildingStatus.Active));
                campaign.Progress = Math.Clamp((temple ? 0.5f : 0) + State.Faith.Faith / 200f, 0, 1);
                if (campaign.Progress >= 1) AdvanceCampaign(CampaignChapter.AgeOfSails);
                break;
            case CampaignChapter.AgeOfSails:
                campaign.Title = "ข้ามขอบฟ้า";
                campaign.Objective = "สร้างกองเรือและเดินทางสู่เมืองอื่น";
                campaign.Progress = Math.Clamp(State.Fleets.Values.Count(f => f.IsActive) / 2f, 0, 1);
                if (campaign.Progress >= 1) AdvanceCampaign(CampaignChapter.ArcaneDiscovery);
                break;
            case CampaignChapter.ArcaneDiscovery:
                campaign.Title = "ความลับแห่งอาคม";
                campaign.Objective = "สำรวจซากโบราณและพัฒนานักเวทระดับ 3";
                float ruin = State.Ruins.Values.Any(r => r.Explored) ? 0.5f : 0;
                float mage = State.Mages.Values.Any(m => m.Level >= 3) ? 0.5f : 0;
                campaign.Progress = ruin + mage;
                if (campaign.Progress >= 1) AdvanceCampaign(CampaignChapter.ChronicleOfAges);
                break;
            case CampaignChapter.ChronicleOfAges:
                campaign.Title = "พงศาวดารแห่งยุคสมัย";
                campaign.Objective = "ดำรงโลกให้ผ่าน 25 ปีและมีประวัติศาสตร์ 25 จุด";
                campaign.Progress = Math.Clamp(Math.Min(_simulation.State.Year / 25f, State.History.Count / 25f), 0, 1);
                if (campaign.Progress >= 1) AdvanceCampaign(CampaignChapter.Completed);
                break;
            case CampaignChapter.Completed:
                campaign.Title = "ผู้สร้างโลก";
                campaign.Objective = "Campaign สำเร็จแล้ว โลกดำเนินต่อใน Sandbox";
                campaign.Progress = 1;
                campaign.ChapterCompleted = true;
                break;
        }
    }

    private void AdvanceCampaign(CampaignChapter next)
    {
        State.Campaign.ChapterCompleted = true;
        AddChronicle("campaign.chapter_complete", "บท Campaign สำเร็จ", State.Campaign.Title, _world.Width / 2, _world.Height / 2, 3);
        State.Campaign = new CampaignProgressState { Chapter = next };
    }

    private void RecordHistory(bool force)
    {
        if (!force && _simulation.State.Day - State.LastHistoryDay < 30) return;
        State.LastHistoryDay = _simulation.State.Day;
        var snapshot = new WorldHistorySnapshot
        {
            Day = _simulation.State.Day,
            Year = _simulation.State.Year,
            Month = _simulation.State.Month,
            Population = _simulation.State.Entities.Values.Count(e => e.IsAlive),
            Settlements = _simulation.State.Settlements.Count,
            Kingdoms = _simulation.State.Kingdoms.Count,
            Armies = _simulation.State.Armies.Values.Count(a => a.IsActive),
            Fleets = State.Fleets.Values.Count(f => f.IsActive),
            Births = _simulation.State.TotalBirths,
            Battles = _simulation.State.TotalBattles,
            Captures = _simulation.State.TotalCitiesCaptured,
            Faith = State.Faith.Faith,
            Fear = State.Faith.Fear,
        };
        foreach (SettlementState city in _simulation.State.Settlements.Values.OrderBy(c => c.Id))
        {
            snapshot.Cities.Add(new HistoryCitySnapshot
            {
                Id = city.Id,
                Name = city.Name,
                X = city.X,
                Y = city.Y,
                KingdomId = city.KingdomId,
                Population = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.SettlementId == city.Id),
                Stage = city.Stage,
                Food = city.Food,
                Happiness = city.Happiness,
            });
        }
        foreach (KingdomState kingdom in _simulation.State.Kingdoms.Values.OrderBy(k => k.Id))
        {
            snapshot.KingdomStates.Add(new HistoryKingdomSnapshot
            {
                Id = kingdom.Id,
                Name = kingdom.Name,
                Race = RaceForKingdom(kingdom.Id),
                Settlements = kingdom.Settlements.Count,
                Population = _simulation.State.Entities.Values.Count(e => e.IsAlive && e.KingdomId == kingdom.Id),
                Stability = kingdom.Stability,
            });
        }
        snapshot.TopLegends.AddRange(State.Legends.Values.OrderByDescending(l => l.Fame + l.Legacy).ThenBy(l => l.EntityId).Take(8).Select(l => l.EntityId));
        State.History.Add(snapshot);
    }

    public string GenerateHistoryReport()
    {
        var lines = new List<string>
        {
            $"พงศาวดารโลก: {_living.State.WorldName}",
            $"Seed: {State.Seed}",
            $"เวลาปัจจุบัน: ปี {_simulation.State.Year} เดือน {_simulation.State.Month} วัน {_simulation.State.Day}",
            $"ประชากร {_simulation.State.Entities.Values.Count(e => e.IsAlive):N0} | เมือง {_simulation.State.Settlements.Count} | อาณาจักร {_simulation.State.Kingdoms.Count}",
            $"ศรัทธา {State.Faith.Faith:0.0} | ความหวาดกลัว {State.Faith.Fear:0.0} | กองเรือ {State.Fleets.Values.Count(f => f.IsActive)}",
            string.Empty,
            "ตำนานสำคัญ",
        };
        foreach (LegendProfile legend in State.Legends.Values.OrderByDescending(l => l.Fame + l.Legacy).Take(20))
        {
            string lifespan = legend.IsDead ? $"เสียชีวิตวันที่ {legend.DeathDay}" : "ยังมีชีวิต";
            lines.Add($"- {DisplayLegendName(legend)} | ชื่อเสียง {legend.Fame} | มรดก {legend.Legacy} | {lifespan}");
            foreach (LegendMemory memory in legend.Memories.OrderByDescending(m => m.Weight).ThenByDescending(m => m.Day).Take(3))
                lines.Add($"  • วัน {memory.Day}: {memory.Summary}");
        }
        lines.Add(string.Empty);
        lines.Add("เหตุการณ์ประวัติศาสตร์");
        foreach (string record in State.WorldLegends.TakeLast(80)) lines.Add($"- {record}");
        lines.Add(string.Empty);
        lines.Add("ภาพรวมตามยุค");
        foreach (WorldHistorySnapshot snapshot in State.History.Where((_, index) => index % 12 == 0 || index == State.History.Count - 1))
            lines.Add($"- ปี {snapshot.Year}: ประชากร {snapshot.Population}, เมือง {snapshot.Settlements}, อาณาจักร {snapshot.Kingdoms}, ศรัทธา {snapshot.Faith:0}");
        return string.Join(Environment.NewLine, lines);
    }
}
