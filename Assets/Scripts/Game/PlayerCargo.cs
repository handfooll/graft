using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Where the player's <see cref="ExtractionLedger"/> lives.
    ///
    /// It needs a home on the player because four separate things reach for it and none of them
    /// should own it: <see cref="EssenceMagnet"/> puts motes in, <see cref="BankZone"/> banks it,
    /// <see cref="PlayerVitals"/> empties it on death, and the HUD draws it. Hanging it off any one
    /// of those would make the other three depend on that one, and would put the rules of the loop
    /// inside whichever component happened to be written first.
    ///
    /// The ledger is built lazily rather than in <c>Awake</c>. It needs the world map to work out
    /// what essence from each area is worth, and <see cref="WorldRegions"/> builds that map in its
    /// own <c>Awake</c> - component order within a scene is not something to depend on, and this is
    /// cheaper than an execution-order attribute that only documents the hazard.
    /// </summary>
    public class PlayerCargo : MonoBehaviour
    {
        [SerializeField] EssenceTuning tuning;
        [SerializeField] WorldRegions regions;

        ExtractionLedger _ledger;
        EssenceValues _values;

        public EssenceTuning Tuning
        {
            get => tuning;
            set
            {
                tuning = value;
                _ledger = null;
            }
        }

        public WorldRegions Regions
        {
            get => regions;
            set
            {
                regions = value;
                _ledger = null;
                _values = null;
            }
        }

        /// <summary>What a unit of cargo from each area banks for. Null until the world exists.</summary>
        public EssenceValues Values
        {
            get
            {
                if (_values != null)
                {
                    return _values;
                }

                var map = regions != null ? regions.Map : null;
                return _values = map != null ? new EssenceValues(map) : null;
            }
        }

        /// <summary>
        /// The loop's arithmetic. Null only if the scene is missing the world or the tuning asset,
        /// which every caller checks for rather than assuming - a null here means the game is
        /// unplayable and should say so once, not throw once per frame.
        /// </summary>
        public ExtractionLedger Ledger
        {
            get
            {
                if (_ledger != null)
                {
                    return _ledger;
                }

                var values = Values;
                if (values == null || tuning == null)
                {
                    return null;
                }

                return _ledger = new ExtractionLedger(values, tuning.cargoCapacity);
            }
        }

        void Start()
        {
            if (Ledger == null)
            {
                Debug.LogError(
                    "[GRAFT] PlayerCargo has no ledger - it is missing its EssenceTuning or its " +
                    "WorldRegions. Nothing can be gathered or banked. Re-run ProjectSetup.ConfigureAll.");
            }
        }
    }
}
