using UnityEngine;

public class UITest : MonoBehaviour
{
    public UIEffectController uiEffectController;
    private int _state = 0;

    public void ClickStateChangeButton()
    {
        ++_state;
        _state %= 3;

        switch (_state)
        {
            case 0:
                uiEffectController.LoadPreset("Default");
                break;
            case 1:
                uiEffectController.LoadPreset("Trauma");
                break;
            case 2:
                uiEffectController.LoadPreset("Awakening");
                break;
        }
    }
}
