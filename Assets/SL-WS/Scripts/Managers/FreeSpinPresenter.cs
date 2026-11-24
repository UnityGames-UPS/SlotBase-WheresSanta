using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The whole free-spin presentation: the trigger popup, the three per-pig intro popups, the
/// background swap, the in-round spin counter and the closing congratulations sequence.
///
/// It owns PRESENTATION only. GameManager still owns the state machine and the server still
/// owns every number — this class is handed a <see cref="FreeSpinRound"/> and animates it.
///
/// Two beats here are unusual and worth knowing before changing anything:
///
///   THE REELS SPIN THROUGH THE INTRO. Once the player dismisses the trigger popup the first
///   free spin is requested immediately and its reels loop, under the black overlay, behind
///   the per-pig popups — for however long those take. <see cref="IsIntroBlocking"/> is what
///   holds GameManager's cosmetic spin timer open, and it is also why this presenter must NOT
///   cancel itself when that spin starts (see <see cref="CancelImmediate"/>).
///
///   THE RED POPUP CLOSES DIFFERENTLY. Blue and Yellow scale down; the wild popup fades out
///   through its CanvasGroup, timed off its glow reaching zero. Its waves animation is
///   REPARENTED out of the popup in Awake so it does not scale with it.
/// </summary>
internal class FreeSpinPresenter : MonoBehaviour
{
  #region Serialized

  [Header("References")]
  [SerializeField] private GameManager gameManager;
  [SerializeField] private UIManager uiManager;
  [SerializeField] private SlotView slotView;


  [Tooltip("Second dismiss button, sitting over the blocked spin button. Same effect as Ok. " +
           "Paired, because the spin button it covers is a different object per orientation.")]
  [SerializeField] private Button startButton;
  [SerializeField] private Button startButtonPortrait;


  [Header("Free Spin Loading")]
  [SerializeField] private GameObject freeSpinLoading;
  [SerializeField] private CanvasGroup freeSpinLoadingCanvasGroup;
  [SerializeField] private TMP_Text multiplierCountText;
  [SerializeField] private TMP_Text freeGamesCountText;
  [SerializeField] private float loadingFadeDuration = 0.5f;
  [SerializeField] private float loadingHoldDuration = 3f;

  [Header("Title References")]
  [SerializeField] private GameObject title;
  [SerializeField] private GameObject titlePortrait;

  [Header("Free Spin End Screen")]
  [SerializeField] private GameObject freeSpinEndScreen;
  [SerializeField] private CanvasGroup freeSpinEndScreenCanvasGroup;
  [SerializeField] private TMP_Text gameWinText;
  [SerializeField] private TMP_Text featureWinText;
  [SerializeField] private TMP_Text totalWinText;
  [SerializeField] private float endScreenFadeDuration = 0.5f;
  [SerializeField] private float textCountUpDuration = 2.5f;
  [SerializeField] private int endScreenDecimals = 2;

  [Header("Take Button")]
  [SerializeField] private Button takeButton;
  [SerializeField] private Button takeButtonPortrait;

  #endregion

  /// <summary>
  /// True while the intro owns the first free spin's reels — GameManager keeps them looping
  /// for as long as this is set, however long the popups take.
  /// </summary>
  internal bool IsIntroBlocking { get; private set; }

  /// <summary>True while any part of this presentation is on screen.</summary>
  internal bool IsActive { get; private set; }

  private Coroutine sequence;

  // The round the outro is closing, so a cancelled outro can still apply its reset meters.
  private FreeSpinRound outroRound;
  private bool dismissRequested;
  private bool collectRequested;
  private bool takeRequested;
  private bool introStopRequested;


  #region Setup

  private void Awake()
  {
    if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
    if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
    if (slotView == null) slotView = FindFirstObjectByType<SlotView>();

    if (freeSpinLoading != null)
    {
      if (freeSpinLoadingCanvasGroup == null)
        freeSpinLoadingCanvasGroup = freeSpinLoading.GetComponent<CanvasGroup>();
      if (freeSpinLoadingCanvasGroup != null)
        freeSpinLoadingCanvasGroup.alpha = 0f;
      SetActive(freeSpinLoading, false);
    }
    AutoDiscoverLoadingTexts();

    AutoUpdateReferncesEndScreen();
    if (freeSpinEndScreen != null)
    {
      if (freeSpinEndScreenCanvasGroup == null)
        freeSpinEndScreenCanvasGroup = freeSpinEndScreen.GetComponent<CanvasGroup>();
      if (freeSpinEndScreenCanvasGroup != null)
        freeSpinEndScreenCanvasGroup.alpha = 0f;
      SetActive(freeSpinEndScreen, false);
    }

    if (startButton != null) startButton.onClick.AddListener(OnDismissPressed);
    if (startButtonPortrait != null) startButtonPortrait.onClick.AddListener(OnDismissPressed);
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

    SetDismissButtons(false);
    SetCollectButton(false);
    SetTakeButton(false);

    ValidateWiring();
  }


  /// <summary>
  /// Report missing references at startup. Every one of these is null-guarded at runtime,
  /// which is exactly why an unwired field would otherwise show up only as a beat that
  /// silently does nothing in the middle of a round.
  /// </summary>
  private void ValidateWiring()
  {
    if (startButton == null || startButtonPortrait == null)
      Debug.LogWarning("[FreeSpins] Only one orientation's start button is assigned, so the " +
                       "other orientation falls back to Ok alone. Assign both startButton " +
                       "and startButtonPortrait.", this);

  }

  private void OnDestroy()
  {
    if (startButton != null) startButton.onClick.RemoveListener(OnDismissPressed);
    if (startButtonPortrait != null) startButtonPortrait.onClick.RemoveListener(OnDismissPressed);
    if (takeButton != null) takeButton.onClick.RemoveListener(OnTakeButtonClicked);
    if (takeButtonPortrait != null) takeButtonPortrait.onClick.RemoveListener(OnTakeButtonClicked);

    KillPopupTweens();
  }

  #endregion

  #region Trigger + intro

  /// <summary>
  /// Start the trigger presentation and return at once. The caller waits by polling
  /// <see cref="IsActive"/> — see <see cref="WaitForCompletion"/>.
  ///
  /// The sequence deliberately runs on THIS component rather than on the caller's coroutine:
  /// a cancel has to be able to stop it, and stopping a coroutine the caller is sitting on a
  /// `yield return StartCoroutine` for would leave the caller waiting forever.
  /// </summary>
  internal void BeginTrigger(FreeSpinRound round)
  {
    if (round == null) return;

    if (sequence != null) StopCoroutine(sequence);
    sequence = StartCoroutine(TriggerRoutine(round));
  }

  /// <summary>
  /// Park until the running sequence finishes or is cancelled. Both exits clear
  /// <see cref="IsActive"/>, so this cannot outlive the presentation.
  /// </summary>
  internal IEnumerator WaitForCompletion()
  {
    while (IsActive) yield return null;
  }

  private IEnumerator TriggerRoutine(FreeSpinRound round)
  {
    IsActive = true;
    uiManager?.OnWinPopupOpened();
    SetTitlesActive(false);

    if (slotView != null)
    {
      float duration = (gameManager != null && gameManager.currentSpinSpeed != SpinSpeed.Normal) ? 1.5f : 2.0f;
      yield return StartCoroutine(slotView.PlayScatterTriggerAnimation(duration));
    }

    yield return StartCoroutine(ShowFreeSpinLoading(round));

    yield return StartCoroutine(WaitForStartButton());


    // From here the reels are spinning and the first result is on its way. IsIntroBlocking is
    // what stops GameManager settling them until the popups below are done.
    IsIntroBlocking = true;
    introStopRequested = false;
    slotView?.ReleaseFeatureOverlay();
    gameManager?.StartFirstFreeSpin();

    IsIntroBlocking = false;


    sequence = null;
    IsActive = false;
    uiManager?.OnWinPopupClosed();
  }

  /// <summary>
  /// Arm or disarm every way of dismissing the trigger popup, so pressing one immediately
  /// kills the others — the popup must not be dismissable twice while it is closing.
  ///
  /// The two kinds are handled DIFFERENTLY on purpose:
  ///
  ///   Ok lives INSIDE the popup and is part of its art, so it only loses its interactivity.
  ///   Deactivating it would make the button pop out of existence while the panel is still
  ///   scaling down around it.
  ///
  ///   The start buttons are overlays sitting over the spin button, outside the popup, so
  ///   they are switched off outright — a merely non-interactable one would go on covering
  ///   the spin button for the whole round.
  ///
  /// Both start buttons are driven, not just the on-screen orientation's, so a rotation while
  /// the popup is up finds the other one already in the right state.
  /// </summary>
  private void SetDismissButtons(bool active)
  {
    // AudioManager.Instance?.PlayFreeSpinRewarded();
    SetButton(startButton, active);
    SetButton(startButtonPortrait, active);
  }

  private static void SetButton(Button button, bool active)
  {
    if (button == null) return;
    SetActive(button.gameObject, active);
    button.interactable = active;
  }

  private void OnDismissPressed()
  {
    if (!dismissRequested)
    {
      dismissRequested = true;
      AudioManager.Instance?.PlayButton();
      // AudioManager.Instance?.PlayFreeSpinRewarded();
    }
  }

  /// <summary>
  /// Cut the intro popups short — the player pressed the normal Stop button, which
  /// GameManager.RequestStop forwards here on every press.
  ///
  /// A no-op outside the intro, so the caller does not have to know whether one is running.
  /// The reels are landed by RequestStop itself; all this adds is unwinding the popups, which
  /// would otherwise go on holding the reels open past the stop the player just asked for.
  /// </summary>
  internal void RequestIntroStop()
  {
    if (!IsIntroBlocking || introStopRequested) return;

    // The popup block sees the flag on its next check and unwinds.
    introStopRequested = true;
  }

  /// <summary>Take every intro popup down this frame, whatever state it was in.</summary>
  private void ResetIntroPopups()
  {
    KillPopupTweens();

  }

  #endregion

  #region Outro

  /// <summary>
  /// Start the closing sequence — congratulations panel with the round's total, the pig UI
  /// reset, the background switched back and the motorboat crossing before control returns.
  /// Wait for it with <see cref="WaitForCompletion"/>.
  /// </summary>
  internal void BeginOutro(FreeSpinRound round)
  {
    if (round == null) return;

    if (sequence != null) StopCoroutine(sequence);
    outroRound = round;
    sequence = StartCoroutine(OutroRoutine(round));
  }

  private IEnumerator OutroRoutine(FreeSpinRound round)
  {
    IsActive = true;
    uiManager?.OnWinPopupOpened();

    slotView?.SettleSymbolsForFeature();

    yield return StartCoroutine(ShowFreeSpinEndScreen(round));

    slotView?.ReleaseFeatureOverlay();

    sequence = null;
    outroRound = null;
    IsActive = false;
    uiManager?.OnWinPopupClosed();
  }


  private void SetCollectButton(bool active)
  {
    SetButton(startButton, active);
    SetButton(startButtonPortrait, active);
  }

  public void OnCollectPressed()
  {
    if (collectRequested) return;

    Debug.Log("[FreeSpins] OnCollectPressed called!");
    collectRequested = true;

      AudioManager.Instance?.PlayButton();
  }

  private IEnumerator ShowFreeSpinLoading(FreeSpinRound round)
  {
    if (freeSpinLoading == null) yield break;

    AutoDiscoverLoadingTexts();

    int spins = round != null && round.totalSpins > 0 ? round.totalSpins : 10;
    int multiplier = GetFreeSpinMultiplier(round);

    if (freeGamesCountText != null)
      SpriteNumberFormatter.Apply(freeGamesCountText, spins, maxDecimals: 0, grouping: false);

    if (multiplierCountText != null)
      SpriteNumberFormatter.Apply(multiplierCountText, multiplier, maxDecimals: 0, grouping: false);

    if (freeSpinLoadingCanvasGroup == null)
      freeSpinLoadingCanvasGroup = freeSpinLoading.GetComponent<CanvasGroup>();

    if (freeSpinLoadingCanvasGroup != null)
      freeSpinLoadingCanvasGroup.alpha = 0f;

    SetActive(freeSpinLoading, true);

    if (freeSpinLoadingCanvasGroup != null)
    {
      yield return freeSpinLoadingCanvasGroup
          .DOFade(1f, loadingFadeDuration)
          .WaitForCompletion();
    }

    Coroutine bgSwitch = null;
    if (uiManager != null)
    {
      bgSwitch = StartCoroutine(uiManager.SwitchBackground(freeSpin: true));
      uiManager.ShowFreeSpinDisplay(round != null ? round.totalSpins : spins);
    }

    yield return new WaitForSeconds(loadingHoldDuration);

    if (bgSwitch != null)
      yield return bgSwitch;

    if (freeSpinLoadingCanvasGroup != null)
    {
      yield return freeSpinLoadingCanvasGroup
          .DOFade(0f, loadingFadeDuration)
          .WaitForCompletion();
    }

    SetActive(freeSpinLoading, false);
  }

  private void SetTitlesActive(bool active)
  {
    if (title != null) title.SetActive(active);
    if (titlePortrait != null) titlePortrait.SetActive(active);
  }

  private int GetFreeSpinMultiplier(FreeSpinRound round)
  {
    if (round != null && round.multiplier > 0)
      return round.multiplier;

    if (gameManager?.gameConfig?.features?.freeGames != null && gameManager.gameConfig.features.freeGames.winMultiplier > 0)
      return gameManager.gameConfig.features.freeGames.winMultiplier;

    return 3;
  }

  private IEnumerator WaitForStartButton()
  {
    dismissRequested = false;
    SetDismissButtons(true);

    while (!dismissRequested) yield return null;

    SetDismissButtons(false);
  }

  private void AutoDiscoverLoadingTexts()
  {
    if (freeSpinLoading == null) return;
    if (freeSpinLoadingCanvasGroup == null)
      freeSpinLoadingCanvasGroup = freeSpinLoading.GetComponent<CanvasGroup>();

    if (multiplierCountText == null || freeGamesCountText == null)
    {
      var texts = freeSpinLoading.GetComponentsInChildren<TMP_Text>(true);
      foreach (var t in texts)
      {
        if (multiplierCountText == null && t.gameObject.name.IndexOf("multiplier", StringComparison.OrdinalIgnoreCase) >= 0)
          multiplierCountText = t;
        else if (freeGamesCountText == null && (t.gameObject.name.IndexOf("freegames", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 t.gameObject.name.IndexOf("count", StringComparison.OrdinalIgnoreCase) >= 0))
          freeGamesCountText = t;
      }
    }
  }

  private IEnumerator ShowFreeSpinEndScreen(FreeSpinRound round)
  {
    yield return new WaitForSeconds(1.0f);

    AutoUpdateReferncesEndScreen();

    if (freeSpinEndScreen == null)
    {
      uiManager?.UpdateBalanceDisplay(force: true);
      SetTitlesActive(true);
      yield break;
    }

    if (freeSpinEndScreenCanvasGroup == null)
      freeSpinEndScreenCanvasGroup = freeSpinEndScreen.GetComponent<CanvasGroup>();

    if (freeSpinEndScreenCanvasGroup != null)
      freeSpinEndScreenCanvasGroup.alpha = 0f;

    SetActive(freeSpinEndScreen, true);
    AudioManager.Instance?.PlayTotalWin();

    if (freeSpinEndScreenCanvasGroup != null)
    {
      yield return freeSpinEndScreenCanvasGroup
          .DOFade(1f, endScreenFadeDuration)
          .WaitForCompletion();
    }

    if (uiManager != null)
    {
      StartCoroutine(uiManager.SwitchBackground(freeSpin: false));
      uiManager.HideFreeSpinDisplay();
    }

    double gameWin = GetGameWinAmount(round);
    double featureWin = GetFeatureWinAmount(round);
    double totalWin = GetTotalWinAmount(round);

    int decimals = endScreenDecimals > 0 ? endScreenDecimals : 2;

    if (gameWinText != null)
      SpriteNumberFormatter.ApplyFixed(gameWinText, 0d, decimals, grouping: false);
    if (featureWinText != null)
      SpriteNumberFormatter.ApplyFixed(featureWinText, 0d, decimals, grouping: false);
    if (totalWinText != null)
      SpriteNumberFormatter.ApplyFixed(totalWinText, 0d, decimals, grouping: false);

    if (gameWin > 0 && gameWinText != null)
      yield return StartCoroutine(CountUpText(gameWinText, gameWin, textCountUpDuration));

    if (featureWin > 0 && featureWinText != null)
      yield return StartCoroutine(CountUpText(featureWinText, featureWin, textCountUpDuration));

    if (totalWin > 0 && totalWinText != null)
      yield return StartCoroutine(CountUpText(totalWinText, totalWin, textCountUpDuration));

    SetDismissButtons(false);
    uiManager?.SetSpinStopButtonStates(isSpinningState: false, isInteractable: false);

    takeRequested = false;
    SetTakeButton(true);

    while (!takeRequested) yield return null;

    SetTakeButton(false);

    uiManager?.UpdateBalanceDisplay(force: true);

    SetTitlesActive(true);

    if (freeSpinEndScreenCanvasGroup != null)
    {
      yield return freeSpinEndScreenCanvasGroup
          .DOFade(0f, endScreenFadeDuration)
          .WaitForCompletion();
    }

    SetActive(freeSpinEndScreen, false);
  }

  private IEnumerator CountUpText(TMP_Text targetText, double targetAmount, float duration)
  {
    if (targetText == null) yield break;

    int decimals = endScreenDecimals > 0 ? endScreenDecimals : 2;
    float elapsed = 0f;
    while (elapsed < duration)
    {
      elapsed += Time.deltaTime;
      double current = targetAmount * Mathf.Clamp01(elapsed / duration);
      SpriteNumberFormatter.ApplyFixed(targetText, current, decimals, grouping: false);
      yield return null;
    }

    SpriteNumberFormatter.ApplyFixed(targetText, targetAmount, decimals, grouping: false);
  }

  private double GetGameWinAmount(FreeSpinRound round)
  {
    if (gameManager?.lastResult != null && gameManager.lastResult.gameWin > 0)
      return gameManager.lastResult.gameWin;

    if (gameManager?.lastResult != null && gameManager.lastResult.baseLineWin > 0)
      return gameManager.lastResult.baseLineWin;

    if (round != null && round.triggerWin > 0)
      return round.triggerWin;

    return 0;
  }

  private double GetFeatureWinAmount(FreeSpinRound round)
  {
    if (gameManager?.lastResult != null && gameManager.lastResult.featureWin > 0)
      return gameManager.lastResult.featureWin;

    if (round != null && round.featureWins > 0)
      return round.featureWins;

    return 0;
  }

  private double GetTotalWinAmount(FreeSpinRound round)
  {
    if (gameManager?.lastResult != null && gameManager.lastResult.totalWin > 0)
      return gameManager.lastResult.totalWin;

    if (round != null && round.accumulatedWin > 0)
      return round.accumulatedWin;

    if (gameManager?.lastResult != null && gameManager.lastResult.grandTotalWin > 0)
      return gameManager.lastResult.grandTotalWin;

    return 0;
  }

  public void OnTakeButtonClicked()
  {
    if (takeRequested) return;
    takeRequested = true;
    AudioManager.Instance?.PlayTakeButton();
    AudioManager.Instance?.StopWin();
  }

  private void SetTakeButton(bool active)
  {
    SetButton(takeButton, active);
    SetButton(takeButtonPortrait, active);
  }

  private void AutoUpdateReferncesEndScreen()
  {
    if (freeSpinEndScreen != null)
    {
      if (freeSpinEndScreenCanvasGroup == null)
        freeSpinEndScreenCanvasGroup = freeSpinEndScreen.GetComponent<CanvasGroup>();

      if (gameWinText == null || featureWinText == null || totalWinText == null)
      {
        var texts = freeSpinEndScreen.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
        {
          string n = t.gameObject.name;
          if (gameWinText == null && n.IndexOf("GameWin", StringComparison.OrdinalIgnoreCase) >= 0)
            gameWinText = t;
          else if (featureWinText == null && n.IndexOf("FeatureWin", StringComparison.OrdinalIgnoreCase) >= 0)
            featureWinText = t;
          else if (totalWinText == null && (n.IndexOf("TotalWin", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Total", StringComparison.OrdinalIgnoreCase) >= 0))
            totalWinText = t;
        }
      }
    }
  }

  #endregion

  #region Teardown

  /// <summary>
  /// Tear the whole presentation down this frame — a new spin has killed it out from under us.
  ///
  /// A NO-OP during the intro. The intro deliberately starts a spin of its own, and that spin
  /// calls SlotView.StartSpin -> KillWinTweens -> here; cancelling then would tear down the
  /// very popups the intro exists to show.
  /// </summary>
  internal void CancelImmediate()
  {
    if (IsIntroBlocking) return;
    if (!IsActive && sequence == null) return;

    // StopAllCoroutines, not just the sequence: stopping an outer coroutine does NOT stop the
    // nested ones it started, and every popup beat is a nested StartCoroutine. Safe only
    // because the sequence lives on THIS component — nobody outside is yielding on it.
    sequence = null;
    StopAllCoroutines();
    ResetIntroPopups();

    if (freeSpinLoadingCanvasGroup != null)
    {
      freeSpinLoadingCanvasGroup.DOKill();
      freeSpinLoadingCanvasGroup.alpha = 0f;
    }
    SetActive(freeSpinLoading, false);
    if (freeSpinEndScreenCanvasGroup != null)
    {
      freeSpinEndScreenCanvasGroup.DOKill();
      freeSpinEndScreenCanvasGroup.alpha = 0f;
    }
    SetActive(freeSpinEndScreen, false);
    SetDismissButtons(false);
    SetCollectButton(false);
    SetTakeButton(false);

    // KillWinTweens released the WIN hold on the darkening panel before calling us, but the
    // feature hold is deliberately immune to that — without this the grid would stay dark for
    // the rest of the session.
    slotView?.ReleaseFeatureOverlay();
    SetTitlesActive(true);

    outroRound = null;

    IsActive = false;
    IsIntroBlocking = false;

    // Drop the gate without touching the controls — the spin that cancelled us has already
    // set the buttons up for spinning.
    uiManager?.OnWinPopupCancelled();
  }

  private void KillPopupTweens()
  {
    freeSpinLoadingCanvasGroup?.DOKill();
    freeSpinEndScreenCanvasGroup?.DOKill();
  }

  private static void SetActive(GameObject target, bool active)
  {
    if (target != null && target.activeSelf != active) target.SetActive(active);
  }

  #endregion
}
