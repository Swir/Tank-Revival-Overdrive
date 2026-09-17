#!/usr/bin/env python3
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
TARGETS=[ROOT/'Assets/Scripts/ObjectiveWarfareV131.cs',ROOT/'Tools/apply_v131_objective_warfare.py']

OLD=r'''        private static ObjectiveArchetypeV131 ResolveKind(int round, int band)
        {
            ObjectiveArchetypeV131 current = RawKind(round, band);
            if (round <= 1) return current;
            ObjectiveArchetypeV131 previous = RawKind(round - 1, band);
            if (current != previous) return current;

            if (round % 10 == 0)
                return current == ObjectiveArchetypeV131.Annihilation ? ObjectiveArchetypeV131.SectorDefense : ObjectiveArchetypeV131.Annihilation;
            if (current == ObjectiveArchetypeV131.ConvoyRescue)
                return ObjectiveArchetypeV131.Counterattack;
            int rotated = ((int)current + 1) % 6;
            return (ObjectiveArchetypeV131)rotated;
        }

        private static ObjectiveArchetypeV131 RawKind(int round, int band)
        {
            int r = Mathf.Clamp(round, 1, PlannedRounds);
            if (r % 10 == 0)
                return ((r / 10) & 1) == 0 ? ObjectiveArchetypeV131.SectorDefense : ObjectiveArchetypeV131.Annihilation;

            if (ConvoyWarfareDirector.HasMissionForRound(r) &&
                ConvoyWarfareDirector.MissionForRound(r) != ConvoyMissionKind.EnemyInterdiction)
                return ObjectiveArchetypeV131.ConvoyRescue;

            int act = Mathf.Clamp((r - 1) / 20, 0, 4);
            int slot = PositiveMod(r * 11 + act * 3 + band * 5, 6);
            return (ObjectiveArchetypeV131)slot;
        }
'''

NEW=r'''        private static ObjectiveArchetypeV131 ResolveKind(int round, int band)
        {
            ObjectiveArchetypeV131 current = RawKind(round, band);
            if (round <= 1) return current;

            // Compare against the fully resolved previous round rather than its raw slot. This keeps
            // anti-repeat correct even when the previous round itself was rotated or convoy-bound.
            ObjectiveArchetypeV131 previous = ResolveKind(round - 1, band);
            if (current != previous) return current;

            if (round % 10 == 0)
                return current == ObjectiveArchetypeV131.Annihilation ? ObjectiveArchetypeV131.SectorDefense : ObjectiveArchetypeV131.Annihilation;
            if (current == ObjectiveArchetypeV131.ConvoyRescue)
                return ObjectiveArchetypeV131.Counterattack;
            return NextGenericKind(current);
        }

        private static ObjectiveArchetypeV131 RawKind(int round, int band)
        {
            int r = Mathf.Clamp(round, 1, PlannedRounds);
            if (r % 10 == 0)
                return ((r / 10) & 1) == 0 ? ObjectiveArchetypeV131.SectorDefense : ObjectiveArchetypeV131.Annihilation;

            // ConvoyRescue is reserved exclusively for a real friendly convoy round. Generic doctrine
            // rotates across the other six archetypes and therefore can never invent a convoy objective.
            if (ConvoyWarfareDirector.HasMissionForRound(r) &&
                ConvoyWarfareDirector.MissionForRound(r) != ConvoyMissionKind.EnemyInterdiction)
                return ObjectiveArchetypeV131.ConvoyRescue;

            int act = Mathf.Clamp((r - 1) / 20, 0, 4);
            int slot = PositiveMod(r * 11 + act * 3 + band * 5, 6);
            return GenericKindForSlot(slot);
        }

        private static ObjectiveArchetypeV131 GenericKindForSlot(int slot)
        {
            switch (PositiveMod(slot, 6))
            {
                case 0: return ObjectiveArchetypeV131.Annihilation;
                case 1: return ObjectiveArchetypeV131.SectorDefense;
                case 2: return ObjectiveArchetypeV131.CommandBreakthrough;
                case 3: return ObjectiveArchetypeV131.EmitterHunt;
                case 4: return ObjectiveArchetypeV131.SupplyInterception;
                default: return ObjectiveArchetypeV131.Counterattack;
            }
        }

        private static ObjectiveArchetypeV131 NextGenericKind(ObjectiveArchetypeV131 current)
        {
            switch (current)
            {
                case ObjectiveArchetypeV131.Annihilation: return ObjectiveArchetypeV131.SectorDefense;
                case ObjectiveArchetypeV131.SectorDefense: return ObjectiveArchetypeV131.CommandBreakthrough;
                case ObjectiveArchetypeV131.CommandBreakthrough: return ObjectiveArchetypeV131.EmitterHunt;
                case ObjectiveArchetypeV131.EmitterHunt: return ObjectiveArchetypeV131.SupplyInterception;
                case ObjectiveArchetypeV131.SupplyInterception: return ObjectiveArchetypeV131.Counterattack;
                default: return ObjectiveArchetypeV131.Annihilation;
            }
        }
'''

for path in TARGETS:
    s=path.read_text(encoding='utf-8')
    count=s.count(OLD)
    if count!=1:
        raise SystemExit(f'{path}: expected one scheduling block, found {count}')
    path.write_text(s.replace(OLD,NEW,1),encoding='utf-8')
    print('patched',path.relative_to(ROOT))
