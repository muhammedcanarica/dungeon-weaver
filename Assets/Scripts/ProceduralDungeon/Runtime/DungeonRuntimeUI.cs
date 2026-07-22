using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralDungeon
{
    public sealed class DungeonRuntimeUI : MonoBehaviour
    {
        [SerializeField] private DungeonRuntimeController runtimeController;
        [SerializeField] private InputField seedInput;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button regenerateButton;
        [SerializeField] private Text statusText;

        private void Awake()
        {
            if (seedInput == null) return;

            seedInput.contentType = InputField.ContentType.Standard;
            seedInput.characterValidation = InputField.CharacterValidation.None;
        }

        private void OnEnable()
        {
            if (generateButton != null) generateButton.onClick.AddListener(GenerateFromInput);
            if (regenerateButton != null) regenerateButton.onClick.AddListener(Regenerate);

            if (runtimeController == null)
            {
                SetStatus("Runtime controller is missing.");
                SetButtonsInteractable(false);
                return;
            }

            runtimeController.StatusChanged += HandleRuntimeStatusChanged;

            if (seedInput != null && string.IsNullOrWhiteSpace(seedInput.text))
                seedInput.text = runtimeController.DefaultSeed.ToString(CultureInfo.InvariantCulture);
            SetStatus(runtimeController.HasSuccessfulDungeon ? runtimeController.LastStatusMessage : "Ready");
            SetButtonsInteractable(!runtimeController.IsGenerating);
        }

        private void OnDisable()
        {
            if (runtimeController != null) runtimeController.StatusChanged -= HandleRuntimeStatusChanged;
            if (generateButton != null) generateButton.onClick.RemoveListener(GenerateFromInput);
            if (regenerateButton != null) regenerateButton.onClick.RemoveListener(Regenerate);
        }

        private void GenerateFromInput()
        {
            if (runtimeController == null || seedInput == null)
            {
                SetStatus("Runtime UI references are missing.");
                return;
            }

            if (!int.TryParse(seedInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
            {
                SetStatus("Invalid seed. Enter a 32-bit integer.");
                return;
            }

            SetStatus($"Generating seed {seed}...");
            SetButtonsInteractable(false);
            runtimeController.GenerateDungeon(seed);
            SetStatus(runtimeController.LastStatusMessage);
            SetButtonsInteractable(!runtimeController.IsGenerating);
        }

        private void Regenerate()
        {
            if (runtimeController == null)
            {
                SetStatus("Runtime controller is missing.");
                return;
            }

            if (!runtimeController.HasSuccessfulDungeon)
            {
                SetStatus("No successful dungeon is available to regenerate.");
                return;
            }

            SetStatus($"Generating seed {runtimeController.LastGeneratedSeed}...");
            SetButtonsInteractable(false);
            runtimeController.RegenerateCurrentSeed();
            SetStatus(runtimeController.LastStatusMessage);
            SetButtonsInteractable(!runtimeController.IsGenerating);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (generateButton != null) generateButton.interactable = interactable;
            if (regenerateButton != null) regenerateButton.interactable = interactable;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void HandleRuntimeStatusChanged(string message)
        {
            SetStatus(message);
            if (runtimeController != null) SetButtonsInteractable(!runtimeController.IsGenerating);
        }
    }
}
