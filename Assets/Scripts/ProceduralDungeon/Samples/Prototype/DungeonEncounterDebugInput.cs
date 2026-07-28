using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonRoomEncounterController))]
    public sealed class DungeonEncounterDebugInput : MonoBehaviour
    {
        [SerializeField] private DungeonRoomEncounterController encounterController;

        private void Awake()
        {
            CacheController();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame) TryClearActiveRoom();
        }

        public bool TryClearActiveRoom()
        {
            return TryClearActiveRoomForSelection(UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null);
        }

        public bool TryClearActiveRoomForSelection(GameObject selectedObject)
        {
            CacheController();
            return encounterController != null
                && !DungeonRuntimeInputFocus.IsInputField(selectedObject)
                && encounterController.TryClearActiveRoom();
        }

        private void CacheController()
        {
            if (encounterController == null)
                encounterController = GetComponent<DungeonRoomEncounterController>();
        }
    }
}
