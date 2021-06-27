﻿using Diplomacy.CivilWar;
using Diplomacy.CivilWar.Factions;
using Diplomacy.Costs;
using Diplomacy.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.EncyclopediaItems;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Diplomacy.ViewModel
{
    internal class RebelFactionsVM : TaleWorlds.Library.ViewModel
    {
        private Action _onComplete;
        private MBBindingList<RebelFactionItemVM> _rebelFactionItems;
        private EncyclopediaFactionVM _kingdomDisplay;
        private bool _shouldShowCreateFaction;
        private Kingdom _kingdom;

        private DiplomacyCost _createFactionCost;
        private HintViewModel _createFactionHint;

        private static readonly TextObject _TCreateFactionLabel = new TextObject("{=hBSo0Ziq}Create Faction");

        [DataSourceProperty]
        public string FactionsLabel { get; set; }

        [DataSourceProperty]
        public string CreateFactionLabel { get; set; }

        [DataSourceProperty]
        public int CreateFactionInfluenceCost { get; set; }

        [DataSourceProperty]
        public string KingdomName { get; set; }

        [DataSourceProperty]
        public HintViewModel HelpHint { get; set; }

        public RebelFactionsVM(Kingdom kingdom, Action onComplete)
        {
            _onComplete = onComplete;
            RebelFactionItems = new();
            FactionsLabel = new TextObject(StringConstants.Factions).ToString();
            CreateFactionLabel = _TCreateFactionLabel.ToString();
            _kingdom = kingdom;
            KingdomName = _kingdom.Name.ToString();
            _createFactionCost = new InfluenceCost(Clan.PlayerClan, Settings.Instance!.FactionCreationInfluenceCost);
            CreateFactionInfluenceCost = Settings.Instance!.FactionCreationInfluenceCost;
            HelpHint = new HintViewModel(GameTexts.FindText("str_faction_help"));
            this.RefreshValues();
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            RebelFactionItems.Clear();
            foreach (RebelFaction rebelFaction in RebelFactionManager.GetRebelFaction(_kingdom))
                RebelFactionItems.Add(new RebelFactionItemVM(rebelFaction, _onComplete, this.RefreshValues));
            var mainHeroIsClanSponsor = RebelFactionItems.Where(factionItem => factionItem.RebelFaction.SponsorClan == Clan.PlayerClan).Any();

            TextObject? reason = null;
            ShouldShowCreateFaction = EligibleForCreateFactionMenu(out reason);
            CreateFactionHint = GenerateCreateFactionHint(reason);
        }

        private bool EligibleForCreateFactionMenu(out TextObject? reason)
        {
            reason = null;

            if (Clan.PlayerClan.Kingdom == null || Clan.PlayerClan.Kingdom != _kingdom)
            {
                reason = new TextObject("{=Xu3rnEEa}Must be part of {KINGDOM_NAME} to create a {KINGDOM_NAME} faction.").SetTextVariable("KINGDOM_NAME", _kingdom.Name);
            }
            else if (_kingdom.IsRebelKingdom())
            {
                reason = new TextObject("{=luvsD6Zn}Cannot create a faction in a rebel kingdom.");
            }
            else if (Clan.PlayerClan.IsUnderMercenaryService)
            {
                reason = new TextObject("{=JDk8ustS}Mercenaries cannot create factions.");
            }
            else if (Clan.PlayerClan == Clan.PlayerClan.Kingdom.RulingClan)
            {
                reason = new TextObject("{=quo5erz6}Rulers cannot create factions.");
            }

            return reason == null;
        }

        private HintViewModel GenerateCreateFactionHint(TextObject? reason)
        {
            var message = _TCreateFactionLabel;
            if (reason != null)
            {
                message = new TextObject("{=GeevRiNR}{LABEL}{newline} {newline}{REASON}").SetTextVariable("LABEL", _TCreateFactionLabel).SetTextVariable("REASON", reason);
            }
            return new HintViewModel(message);
        }

        public void OnComplete() => _onComplete();

        public void OnCreateFaction()
        {
            InformationManager.HideInformations();
            List<InquiryElement> inquiryElements = new();
            foreach (int value in Enum.GetValues(typeof(RebelDemandType)))
            {
                var demandType = (RebelDemandType)value;
                var canCreate = CreateFactionAction.CanApply(Clan.PlayerClan, demandType, out TextObject? reason);
                string hint = demandType.GetHint();
                if (reason != null)
                {
                    hint = new TextObject("{=ALSuNVzE}{EXCEPTION}{newline} {newline}{DEMAND_HINT}")
                        .SetTextVariable("EXCEPTION", reason)
                        .SetTextVariable("DEMAND_HINT", hint)
                        .ToString();
                }
                inquiryElements.Add(new InquiryElement(demandType, demandType.GetName(), null, canCreate, hint));
            }

            GameTexts.SetVariable("INFLUENCE_ICON", "{=!}<img src=\"Icons\\Influence@2x\" extend=\"7\">");

            InformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                CreateFactionLabel,
                new TextObject("{=2PglxF8k}Choose a faction demand to create a faction.{newline}Cost: {INFLUENCE_COST}{INFLUENCE_ICON}").SetTextVariable("INFLUENCE_COST", CreateFactionInfluenceCost).ToString(),
                inquiryElements,
                true,
                1,
                GameTexts.FindText("str_ok").ToString(),
                GameTexts.FindText("str_cancel").ToString(),
                this.HandleCreateFaction,
                null
                ), true);
        }

        private void HandleCreateFaction(List<InquiryElement> inquiryElements)
        {
            object identifier = inquiryElements.First().Identifier;

            // canceled 
            if (identifier == null)
            {
                return;
            }

            var rebelDemandType = (RebelDemandType)identifier;

            RebelFaction rebelFaction;

            switch (rebelDemandType)
            {
                case RebelDemandType.Secession:
                    rebelFaction = new SecessionFaction(Clan.PlayerClan);
                    break;
                case RebelDemandType.Abdication:
                    rebelFaction = new AbdicationFaction(Clan.PlayerClan);
                    break;
                default:
                    throw new MBException("Should have a type of demand when creating a faction");
            }

            CreateFactionAction.Apply(rebelFaction);
            this.RefreshValues();
        }

        [DataSourceProperty]
        public MBBindingList<RebelFactionItemVM> RebelFactionItems
        {
            get => _rebelFactionItems;
            set
            {
                if (value != _rebelFactionItems)
                {
                    _rebelFactionItems = value;
                    OnPropertyChanged(nameof(RebelFactionItems));
                }
            }
        }

        [DataSourceProperty]
        public EncyclopediaFactionVM KingdomDisplay
        {
            get => _kingdomDisplay;
            set
            {
                if (value != _kingdomDisplay)
                {
                    _kingdomDisplay = value;
                    OnPropertyChanged(nameof(KingdomDisplay));
                }
            }
        }

        [DataSourceProperty]
        public bool ShouldShowCreateFaction
        {
            get => _shouldShowCreateFaction;
            set
            {
                if (value != _shouldShowCreateFaction)
                {
                    _shouldShowCreateFaction = value;
                    OnPropertyChanged(nameof(ShouldShowCreateFaction));
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel CreateFactionHint
        {
            get => _createFactionHint;
            set
            {
                if (value != _createFactionHint)
                {
                    _createFactionHint = value;
                    OnPropertyChanged(nameof(CreateFactionHint));
                }
            }
        }
    }
}