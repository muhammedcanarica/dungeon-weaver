using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProceduralDungeon
{
    public static class DungeonRuntimeInputFocus
    {
        public static bool IsInputFieldFocused()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            return IsInputField(selected);
        }

        public static bool IsInputField(GameObject selectedObject)
        {
            if (selectedObject == null) return false;
            if (selectedObject.TryGetComponent(out InputField _)) return true;
            return selectedObject.GetComponentInParent<InputField>(true) != null;
        }
    }
}
