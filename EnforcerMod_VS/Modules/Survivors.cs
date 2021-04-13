﻿using RoR2;
using UnityEngine;

namespace EnforcerPlugin.Modules
{
    internal static class Survivors
    {
        internal static void RegisterNewSurvivor(GameObject bodyPrefab, GameObject displayPrefab, string namePrefix, UnlockableDef unlockableDef, float sortPosition)
        {
            string fullNameString = namePrefix + "_NAME";
            string fullDescString = namePrefix + "_DESCRIPTION";
            string fullOutroString = namePrefix + "_OUTRO_FLAVOR";
            string fullFailureString = namePrefix + "_OUTRO_FAILURE";

            SurvivorDef survivorDef = ScriptableObject.CreateInstance<SurvivorDef>();
            survivorDef.bodyPrefab = bodyPrefab;
            survivorDef.displayPrefab = displayPrefab;
            survivorDef.displayNameToken = fullNameString;
            survivorDef.descriptionToken = fullDescString;
            survivorDef.outroFlavorToken = fullOutroString;
            survivorDef.mainEndingEscapeFailureFlavorToken = fullFailureString;
            survivorDef.desiredSortPosition = sortPosition;
            survivorDef.unlockableDef = unlockableDef;

            EnforcerPlugin.survivorDefs.Add(survivorDef);
        }

        internal static void RegisterNewSurvivor(GameObject bodyPrefab, GameObject displayPrefab, string namePrefix) { RegisterNewSurvivor(bodyPrefab, displayPrefab, namePrefix, null, 4f); }
    }
}