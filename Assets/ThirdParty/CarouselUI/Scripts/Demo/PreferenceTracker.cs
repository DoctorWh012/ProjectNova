using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CarouselUI.Demo
{
    public class PreferenceTracker : MonoBehaviour
    {
        void Awake()
        {
            StartSetup(); // SETS UP PLAYER PREFS, IF PLAYER PREFS ARE NONEXISTENT IT WILL CREATE THEM.
        }

        private void StartSetup()
        {
            if (!PlayerPrefs.HasKey(PreferenceEnum.AO.ToString()))
            { PlayerPrefs.SetInt(PreferenceEnum.AO.ToString(), 0); }

            if (!PlayerPrefs.HasKey(PreferenceEnum.AntiAlias.ToString()))
            { PlayerPrefs.SetInt(PreferenceEnum.AntiAlias.ToString(), 0); }

            if (!PlayerPrefs.HasKey(PreferenceEnum.DeveloperComm.ToString()))
            { PlayerPrefs.SetInt(PreferenceEnum.DeveloperComm.ToString(), 0); }

            if (!PlayerPrefs.HasKey(PreferenceEnum.DifficultyMode.ToString()))
            { PlayerPrefs.SetInt(PreferenceEnum.DifficultyMode.ToString(), 0); }

            if (!PlayerPrefs.HasKey(PreferenceEnum.LanguageSub.ToString()))
            { PlayerPrefs.SetInt(PreferenceEnum.LanguageSub.ToString(), 0); }

            if (!PlayerPrefs.HasKey(PreferenceEnum.LanguageVO.ToString()))
            { PlayerPrefs.SetInt(PreferenceEnum.LanguageVO.ToString(), 0); }
        }

        /// <summary>
        /// Prints saved preferences to display changes.
        /// </summary>
        [ContextMenu("Print All Preferences")]
        public void PrintAllPreferences()
        {
            print($"{PreferenceEnum.AO.ToString()} is {GetValues(PreferenceEnum.AO)}");
            print($"{PreferenceEnum.AntiAlias.ToString()} is {GetValues(PreferenceEnum.AntiAlias)}");
            print($"{PreferenceEnum.DeveloperComm.ToString()} is {GetValues(PreferenceEnum.DeveloperComm)}");
            print($"{PreferenceEnum.DifficultyMode.ToString()} is {GetValues(PreferenceEnum.DifficultyMode)}");
            print($"{PreferenceEnum.LanguageSub.ToString()} is {GetValues(PreferenceEnum.LanguageSub)}");
            print($"{PreferenceEnum.LanguageVO.ToString()} is {GetValues(PreferenceEnum.LanguageVO)}");
        }

        /// <summary>
        /// Deletes all preferences saved.
        /// </summary>
        [ContextMenu("Remove All Preferences")]
        public void RemoveAllPreferences()
        {
            PlayerPrefs.DeleteKey(PreferenceEnum.AntiAlias.ToString());
            PlayerPrefs.DeleteKey(PreferenceEnum.AO.ToString());
            PlayerPrefs.DeleteKey(PreferenceEnum.DeveloperComm.ToString());
            PlayerPrefs.DeleteKey(PreferenceEnum.DifficultyMode.ToString());
            PlayerPrefs.DeleteKey(PreferenceEnum.LanguageSub.ToString());
            PlayerPrefs.DeleteKey(PreferenceEnum.LanguageVO.ToString());
        }

        /// <summary>
        /// Resets all preference values back to 0
        /// </summary>
        public void ResetAllPeferenceToZero()
        {
            SetValues(PreferenceEnum.AntiAlias, 0);
            SetValues(PreferenceEnum.AO, 0);
            SetValues(PreferenceEnum.DeveloperComm, 0);
            SetValues(PreferenceEnum.DifficultyMode, 0);
            SetValues(PreferenceEnum.LanguageSub, 0);
            SetValues(PreferenceEnum.LanguageVO, 0);
        }

        public int GetValues(PreferenceEnum preference)
        {
            return PlayerPrefs.GetInt(preference.ToString());
        }

        public void SetValues(PreferenceEnum preference, int value)
        {
            PlayerPrefs.SetInt(preference.ToString(), value);
        }
    }
}