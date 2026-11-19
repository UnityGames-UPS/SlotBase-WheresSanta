using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class BonusManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject christmasGift;
    [SerializeField] private GameObject darkOverlay;
    [SerializeField] private Button startButton;
    [SerializeField] private Button startButtonPortrait;
    [SerializeField] private Button takeButton;
    [SerializeField] private Button takeButtonPortrait;
    [SerializeField] private Button spinButton;
    [SerializeField] private Button spinButtonPortrait;

    [Header("Background References")]
    [SerializeField] private GameObject backgroundLan;
    [SerializeField] private GameObject backgroundPort;

    [Header("Title References")]
    [SerializeField] private GameObject title;
    [SerializeField] private GameObject titlePortrait;

    [Header("Bonus Layout Settings")]
    [SerializeField] private RectTransform bonusGameUiButtons;
    [SerializeField] private RectTransform totalWin;
    [SerializeField] private Vector3 portraitUiScale = new Vector3(0.72f, 0.72f, 0.72f);
    [SerializeField] private Vector3 landscapeUiScale = Vector3.one;
    [SerializeField] private float portraitUiPosY = -280f;
    [SerializeField] private float landscapeUiPosY = 0f;
    [SerializeField] private GameObject SantaHasLeftGifts;
    [SerializeField] private GameObject pickGift;
    [SerializeField] private TMP_Text pickGiftText;
    [SerializeField] private GameObject bonusWin;
    [SerializeField] private TMP_Text bonusWinText;
    [SerializeField] private GameObject pickMoreGift;
    [SerializeField] private GameObject pickLastGift;

    [Header("Total Win Screen References")]
    [SerializeField] private CanvasGroup totalWinScreenBg;
    [SerializeField] private GameObject totalWinScreen;
    [SerializeField] private TMP_Text totalWinText;
    [SerializeField] private float totalWinFadeDuration = 0.5f;
    [SerializeField] private float countUpDuration = 1.5f;

    [Header("Gift Buttons & Effects")]
    [SerializeField] private Button[] giftButtons;
    [SerializeField] private GameObject[] starEffects;
    [SerializeField] private TMP_Text[] winGiftWinAmountTexts;
    [SerializeField] private GameObject[] remainingTexts;
    [SerializeField] private GameObject remainingText;

    [Header("Managers")]
    [SerializeField] internal GameManager gameManager;
    [SerializeField] internal UIManager uiManager;

    [SerializeField] internal SocketIOManager socketIOManager;

    private int pickCount;
    private int giftCount;
    private int picksMade;
    private bool isOver;
    private bool hasRevealedRemaining;
    private bool waitingForResult;
    private bool takeRequested;
    private Tween totalWinTween;

    internal double Totalwinamount = 0;

    private void OnEnable()
    {
        OrientationChange.OnOrientationChanged += HandleOrientationChanged;
    }

    private void OnDisable()
    {
        OrientationChange.OnOrientationChanged -= HandleOrientationChanged;
    }

    private void HandleOrientationChanged(OrientationChange.OrientationMode mode, int width, int height)
    {
        bool isPortrait = (mode == OrientationChange.OrientationMode.MobilePortrait);
        UpdateBonusOrientationLayout(isPortrait);
    }

    private void Start()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();

        EnsureBackgroundReferences();
        UpdateBonusOrientationLayout(IsPortraitMode());

        if (christmasGift != null && christmasGift.transform.parent != null && christmasGift.transform.parent.name == "LandScapeObj")
        {
            christmasGift.transform.SetParent(christmasGift.transform.parent.parent, false);
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartButtonClicked);
            startButton.onClick.AddListener(OnStartButtonClicked);
        }
        if (startButtonPortrait != null)
        {
            startButtonPortrait.onClick.RemoveListener(OnStartButtonClicked);
            startButtonPortrait.onClick.AddListener(OnStartButtonClicked);
        }
        if (takeButton != null)
        {
            takeButton.onClick.RemoveListener(OnTakeButtonClicked);
            takeButton.onClick.AddListener(OnTakeButtonClicked);
        }
        if (takeButtonPortrait != null)
        {
            takeButtonPortrait.onClick.RemoveListener(OnTakeButtonClicked);
            takeButtonPortrait.onClick.AddListener(OnTakeButtonClicked);
        }
        SetTakeButton(false);

        if (giftButtons != null)
        {
            for (int i = 0; i < giftButtons.Length; i++)
            {
                int buttonIndex = i;
                if (giftButtons[buttonIndex] != null)
                {
                    giftButtons[buttonIndex].onClick.RemoveAllListeners();
                    giftButtons[buttonIndex].onClick.AddListener(() => OnGiftButtonClicked(buttonIndex));
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartButtonClicked);
        if (startButtonPortrait != null) startButtonPortrait.onClick.RemoveListener(OnStartButtonClicked);
        if (takeButton != null) takeButton.onClick.RemoveListener(OnTakeButtonClicked);
        if (takeButtonPortrait != null) takeButtonPortrait.onClick.RemoveListener(OnTakeButtonClicked);
    }


    public void InitializeBonusData(int picks, int gifts)
    {
        pickCount = picks;
        giftCount = gifts;
    }

    [ContextMenu("Start Bonus")]
    public void StartBonus()
    {
        if (gameManager != null && gameManager.isAutoPlaying)
            gameManager.StopAutoPlay();

        picksMade = 0;
        isOver = false;
        hasRevealedRemaining = false;
        waitingForResult = false;
        takeRequested = false;
        Totalwinamount = 0;

        SetTitlesActive(false);
        UpdateBonusOrientationLayout(IsPortraitMode());

        if (christmasGift != null) christmasGift.SetActive(true);
        if (darkOverlay != null) darkOverlay.SetActive(true);
        SetStartButton(true);
        SetTakeButton(false);
        SetSpinButtonInteractable(false);

        if (SantaHasLeftGifts != null) SantaHasLeftGifts.SetActive(false);
        if (pickGift != null) pickGift.SetActive(false);
        if (pickGiftText != null) pickGiftText.gameObject.SetActive(false);
        if (bonusWin != null) bonusWin.SetActive(false);
        if (pickMoreGift != null) pickMoreGift.SetActive(false);
        if (pickLastGift != null) pickLastGift.SetActive(false);
        if (bonusWinText != null)
        {
            bonusWinText.text = ConvertToSpriteFont(0);
            bonusWinText.gameObject.SetActive(false);
        }

        if (totalWinScreenBg != null)
        {
            totalWinScreenBg.alpha = 0f;
            totalWinScreenBg.gameObject.SetActive(false);
        }
        if (totalWinScreen != null) totalWinScreen.SetActive(false);
        if (totalWinText != null)
        {
            totalWinText.text = ConvertToSpriteFontFixed(0, 2);
            totalWinText.gameObject.SetActive(false);
        }

        if (giftButtons != null)
        {
            for (int i = 0; i < giftButtons.Length; i++)
            {
                if (giftButtons[i] != null)
                {
                    giftButtons[i].interactable = true;
                    giftButtons[i].gameObject.SetActive(false);
                }
            }
        }

        if (starEffects != null)
            foreach (var fx in starEffects) if (fx != null) fx.SetActive(false);

        if (winGiftWinAmountTexts != null)
            foreach (var t in winGiftWinAmountTexts) if (t != null) t.gameObject.SetActive(false);

        if (remainingTexts != null)
            foreach (var r in remainingTexts) if (r != null) r.SetActive(false);

        if (remainingText != null) remainingText.SetActive(false);
    }


    private void OnStartButtonClicked()
    {
        SetStartButtonInteractable(false);
        if (darkOverlay != null) darkOverlay.SetActive(false);
        if (SantaHasLeftGifts != null) SantaHasLeftGifts.SetActive(true);

        StartCoroutine(RevealGiftsSequentially());
    }

    private IEnumerator RevealGiftsSequentially()
    {
        int buttonsToShow = giftCount > 0 ? Mathf.Min(giftCount, giftButtons.Length)
                                           : giftButtons.Length;

        for (int i = 0; i < buttonsToShow; i++)
        {
            if (giftButtons != null && i < giftButtons.Length && giftButtons[i] != null)
            {
                giftButtons[i].gameObject.SetActive(true);
                giftButtons[i].interactable = true;
            }
            yield return new WaitForSeconds(0.2f);
        }

        if (SantaHasLeftGifts != null) SantaHasLeftGifts.SetActive(false);
        if (pickGift != null) pickGift.SetActive(true);
        if (pickGiftText != null)
        {
            pickGiftText.text = ConvertToSpriteFont(pickCount);
            pickGiftText.gameObject.SetActive(true);
        }
    }

    private void OnGiftButtonClicked(int buttonIndex)
    {
        if (isOver) return;
        if (waitingForResult) return;

        if (giftButtons == null || buttonIndex < 0 || buttonIndex >= giftButtons.Length) return;
        if (giftButtons[buttonIndex] == null || !giftButtons[buttonIndex].gameObject.activeSelf) return;

        SetAllGiftButtonsInteractable(false);
        waitingForResult = true;

        if (socketIOManager != null)
        {
            socketIOManager.SendBonusRequest(buttonIndex);
        }
        else
        {
            Debug.LogWarning("[BonusManager] socketIOManager is null — cannot send bonus request.");
            SetAllGiftButtonsInteractable(true);
            waitingForResult = false;
        }
    }

    public void OnBonusResult(BonusResultPayload payload)
    {
        waitingForResult = false;

        if (payload == null) return;

        int confirmedIndex = payload.giftIndex;
        isOver = payload.isOver;
        picksMade = payload.picksMade;
        Totalwinamount = payload.currentWinning;

        RevealGift(confirmedIndex, payload.prizeWin);

        int remaining = pickCount - picksMade;
        UpdatePickPrompt(remaining, picksMade);

        UpdateWinText();

        if (isOver)
        {
            StartCoroutine(FinishBonusAfterDelay());
        }
        else
        {
            ReenableUnselectedButtons(payload.selectedIndexes);
        }
    }


    private void RevealGift(int buttonIndex, double prizeWin)
    {
        if (giftButtons == null || buttonIndex < 0 || buttonIndex >= giftButtons.Length) return;

        var btn = giftButtons[buttonIndex];
        if (btn != null)
        {
            btn.interactable = false;
            btn.gameObject.SetActive(false);
        }

        if (starEffects != null && buttonIndex < starEffects.Length && starEffects[buttonIndex] != null)
            starEffects[buttonIndex].SetActive(true);

        if (winGiftWinAmountTexts != null && buttonIndex < winGiftWinAmountTexts.Length
            && winGiftWinAmountTexts[buttonIndex] != null)
        {
            var txt = winGiftWinAmountTexts[buttonIndex];
            txt.transform.localScale = Vector3.zero;
            txt.text = ConvertToSpriteFont(prizeWin);
            txt.gameObject.SetActive(true);
            txt.transform.DOScale(Vector3.one, 1.0f);
        }
    }

    private void ReenableUnselectedButtons(List<int> selectedIndexes)
    {
        if (giftButtons == null) return;

        int buttonsToShow = giftCount > 0 ? Mathf.Min(giftCount, giftButtons.Length)
                                           : giftButtons.Length;

        for (int i = 0; i < buttonsToShow; i++)
        {
            if (giftButtons[i] == null) continue;
            if (!giftButtons[i].gameObject.activeSelf) continue;

            bool wasSelected = selectedIndexes != null && selectedIndexes.Contains(i);
            giftButtons[i].interactable = !wasSelected;
        }
    }


    private void UpdatePickPrompt(int remaining, int made)
    {
        if (made >= 1)
        {
            if (pickGift != null) pickGift.SetActive(false);
            if (bonusWin != null) bonusWin.SetActive(true);
            if (bonusWinText != null) bonusWinText.gameObject.SetActive(true);
        }

        if (pickGiftText != null)
            pickGiftText.text = ConvertToSpriteFont(remaining);

        if (remaining == 1 && !isOver)
        {
            if (pickMoreGift != null) pickMoreGift.SetActive(false);
            if (pickLastGift != null) pickLastGift.SetActive(true);
        }
        else if (remaining > 1)
        {
            if (pickMoreGift != null) pickMoreGift.SetActive(true);
            if (pickLastGift != null) pickLastGift.SetActive(false);
        }
        else
        {
            if (pickMoreGift != null) pickMoreGift.SetActive(false);
            if (pickLastGift != null) pickLastGift.SetActive(false);
            if (pickGiftText != null) pickGiftText.gameObject.SetActive(false);
        }
    }

    internal void UpdateWinText()
    {
        if (bonusWinText != null)
            bonusWinText.text = ConvertToSpriteFont(Totalwinamount);
    }


    private IEnumerator FinishBonusAfterDelay()
    {
        yield return new WaitForSeconds(0.6f);
        RevealRemainingGifts();
    }

    private void RevealRemainingGifts()
    {
        if (hasRevealedRemaining) return;
        hasRevealedRemaining = true;

        if (pickGiftText != null) pickGiftText.gameObject.SetActive(false);

        int buttonsToShow = giftCount > 0 ? Mathf.Min(giftCount, giftButtons.Length)
                                           : giftButtons.Length;

        if (giftButtons != null)
        {
            for (int i = 0; i < buttonsToShow; i++)
            {
                if (giftButtons[i] == null) continue;
                if (!giftButtons[i].gameObject.activeSelf) continue;

                giftButtons[i].interactable = false;
                giftButtons[i].gameObject.SetActive(false);

                if (remainingTexts != null && i < remainingTexts.Length && remainingTexts[i] != null)
                {
                    remainingTexts[i].transform.localScale = Vector3.zero;
                    remainingTexts[i].SetActive(true);
                    remainingTexts[i].transform.DOScale(Vector3.one, 1.0f);
                }
            }
        }

        if (remainingText != null)
        {
            remainingText.transform.localScale = Vector3.zero;
            remainingText.SetActive(true);
            remainingText.transform.DOScale(Vector3.one, 1.0f);
        }

        StartCoroutine(ShowTotalWinSequence());
    }

    private IEnumerator ShowTotalWinSequence()
    {
        yield return new WaitForSeconds(1.0f);

        if (totalWinScreenBg != null)
        {
            totalWinScreenBg.alpha = 0f;
            totalWinScreenBg.gameObject.SetActive(true);
            totalWinScreenBg.DOFade(1f, totalWinFadeDuration);
        }

        if (totalWinScreen != null) totalWinScreen.SetActive(true);

        if (totalWinText != null)
        {
            totalWinText.text = ConvertToSpriteFontFixed(0, 2);
            totalWinText.gameObject.SetActive(true);
            float startVal = 0f;
            float endVal = (float)Totalwinamount;
            totalWinTween = DOTween.To(() => startVal, x =>
            {
                startVal = x;
                totalWinText.text = ConvertToSpriteFontFixed(x, 2);
            }, endVal, countUpDuration).OnComplete(() =>
            {
                totalWinText.text = ConvertToSpriteFontFixed(Totalwinamount, 2);
                totalWinTween = null;
                ShowTakeButtonAfterBonus();
            });
        }
        else
        {
            ShowTakeButtonAfterBonus();
        }
    }

    private void ShowTakeButtonAfterBonus()
    {
        SetStartButton(false);
        SetTakeButton(true);
    }

    private void OnTakeButtonClicked()
    {
        if (takeRequested) return;
        takeRequested = true;

        if (totalWinTween != null && totalWinTween.IsActive())
        {
            totalWinTween.Kill();
            totalWinTween = null;
            if (totalWinText != null)
                totalWinText.text = ConvertToSpriteFontFixed(Totalwinamount, 2);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayTakeButton();

        SetTakeButton(false);
        SetStartButton(false);
        SetTitlesActive(true);

        if (totalWinScreenBg != null) totalWinScreenBg.gameObject.SetActive(false);
        if (totalWinScreen != null) totalWinScreen.SetActive(false);
        if (totalWinText != null) totalWinText.gameObject.SetActive(false);
        if (christmasGift != null) christmasGift.SetActive(false);
        SetSpinButtonInteractable(true);

        if (gameManager != null)
            gameManager.OnBonusComplete();

        if (uiManager != null)
            uiManager.UpdateBalanceDisplay(force: true);
    }

    public void UpdateBonusOrientationLayout(bool isPortrait)
    {
        EnsureBackgroundReferences();
        if (backgroundPort != null) backgroundPort.SetActive(isPortrait);
        if (backgroundLan != null) backgroundLan.SetActive(!isPortrait);

        if (bonusGameUiButtons != null)
        {
            bonusGameUiButtons.localScale = isPortrait ? portraitUiScale : landscapeUiScale;
            Vector2 pos = bonusGameUiButtons.anchoredPosition;
            pos.y = isPortrait ? portraitUiPosY : landscapeUiPosY;
            bonusGameUiButtons.anchoredPosition = pos;
        }

        if (totalWin != null)
        {
            totalWin.localScale = isPortrait ? portraitUiScale : landscapeUiScale;
            Vector2 pos = totalWin.anchoredPosition;
            pos.y = isPortrait ? portraitUiPosY : landscapeUiPosY;
            totalWin.anchoredPosition = pos;
        }

        if (gameManager != null && gameManager.isInBonus)
        {
            SetSpinButtonInteractable(false);
            SetTitlesActive(false);
        }
    }

    public bool IsPortraitMode()
    {
        var oc = FindObjectOfType<OrientationChange>();
        if (oc != null)
        {
            return oc.CurrentMode == OrientationChange.OrientationMode.MobilePortrait;
        }
        return Screen.width < Screen.height;
    }

    private void SetSpinButtonInteractable(bool interactable)
    {
        if (spinButton != null) spinButton.interactable = interactable;
        if (spinButtonPortrait != null) spinButtonPortrait.interactable = interactable;
    }

    private void SetTitlesActive(bool active)
    {
        if (title != null) title.SetActive(active);
        if (titlePortrait != null) titlePortrait.SetActive(active);
    }

    private void EnsureBackgroundReferences()
    {
    }

    private void SetStartButton(bool active)
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(active);
            startButton.interactable = active;
        }
        if (startButtonPortrait != null)
        {
            startButtonPortrait.gameObject.SetActive(active);
            startButtonPortrait.interactable = active;
        }
    }

    private void SetStartButtonInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
        if (startButtonPortrait != null) startButtonPortrait.interactable = interactable;
    }

    private void SetTakeButton(bool active)
    {
        if (takeButton != null)
        {
            takeButton.gameObject.SetActive(active);
            takeButton.interactable = active;
        }
        if (takeButtonPortrait != null)
        {
            takeButtonPortrait.gameObject.SetActive(active);
            takeButtonPortrait.interactable = active;
        }
    }


    private void SetAllGiftButtonsInteractable(bool interactable)
    {
        if (giftButtons == null) return;
        foreach (var btn in giftButtons)
            if (btn != null && btn.gameObject.activeSelf)
                btn.interactable = interactable;
    }

    internal void GameOver()
    {
        OnTakeButtonClicked();
    }


    private string ConvertToSpriteFont(double val) => ConvertToSpriteFont(val.ToString("0.##"));
    private string ConvertToSpriteFont(int val) => ConvertToSpriteFont(val.ToString());

    private string ConvertToSpriteFontFixed(double val, int decimals)
    {
        string pattern = "0" + (decimals > 0 ? "." + new string('0', decimals) : string.Empty);
        return ConvertToSpriteFont(val.ToString(pattern, System.Globalization.CultureInfo.InvariantCulture));
    }

    private string ConvertToSpriteFont(string val, bool includeCurrency = false)
    {
        if (string.IsNullOrEmpty(val)) val = "0";
        if (val.Contains("<sprite=")) return val;

        val = val.Trim();
        bool hasDollar = val.StartsWith("$") || includeCurrency;
        if (val.StartsWith("$")) val = val.Substring(1).Trim();

        string cleanVal = val.Replace(",", "");
        string intPart = cleanVal;
        string decPart = null;
        int dotIndex = cleanVal.IndexOf('.');
        if (dotIndex >= 0)
        {
            intPart = cleanVal.Substring(0, dotIndex);
            decPart = cleanVal.Substring(dotIndex + 1);
        }

        if (double.TryParse(intPart, out double intVal))
            intPart = intVal.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture);

        string formatted = (hasDollar ? "$" : string.Empty) + intPart + (decPart != null ? "." + decPart : string.Empty);
        var sb = new System.Text.StringBuilder(formatted.Length * 12);
        for (int i = 0; i < formatted.Length; i++)
        {
            char c = formatted[i];
            if (c >= '0' && c <= '9')
                sb.Append("<sprite=").Append(c - '0').Append('>');
            else if (c == '.')
                sb.Append("<sprite=10>");
            else if (c == ',')
                sb.Append("<sprite=11>");
            else if (c == '$')
                sb.Append("<sprite=12>");
        }
        return sb.ToString();
    }
}
