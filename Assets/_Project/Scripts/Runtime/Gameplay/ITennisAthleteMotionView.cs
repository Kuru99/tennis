using PrideCourt.Domain;
using UnityEngine;

namespace PrideCourt.Gameplay
{
    public interface ITennisAthleteMotionView
    {
        void PlayServeToss();
        void PlayStroke(ShotPower power, float horizontalAim, StrokeMotion motion);
        void PlayDive(Vector2 direction);
    }
}
