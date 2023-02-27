using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CarouselUI.Demo;

namespace CarouselUI.Demo
{
    public class CarouselUIProcessor_PlayerPrefs : CarouselUI_Processor_Base
    {
        [SerializeField, Tooltip("Tracker gameobject, necessary for updating and tracking.")] private PreferenceTracker _prefTrackerInstance;
        [SerializeField, Tooltip("Playerpref setting to set by this controller.")] private PreferenceEnum _settingToInfluence;

        protected override void UpdateCarouselUI()
        {
            //STORES VALUES ON THIS INSTANCE OF THE SCRIPT, FIRED ONENABLE & WHENEVER CAROUSEL UPDATES
            _storedSettingsIndex = _prefTrackerInstance.GetValues(_settingToInfluence);

            base.UpdateCarouselUI(); //NEEDED BECAUSE THIS ALLOWS UPDATING OF THE ASSOCIATED CAROUSEL ELEMENT INDEX
        }

        protected override void DetermineOutput(int input)
        {
            //THIS OUTPUTS THE NEW VALUE FROM THE CAROUSEL
            _prefTrackerInstance.SetValues(_settingToInfluence, input);

            //BASE VERSION NOT REFERENCED HERE AS IT ONLY CONTAINS A PRINT
        }
    }
}