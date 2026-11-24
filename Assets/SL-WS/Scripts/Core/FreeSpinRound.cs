using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One free-spin round's bookkeeping — the part the server does NOT send.
///
/// Rich Piggies reports only payload.freeSpinsRemaining. There is no round total, no
/// spins-used and no round-win total in the payload (the converter leaves serverTotalSpins
/// and serverSpinsUsed at 0, and serverTotalRoundWin at the current spin's own win), so the
/// client latches the total from the trigger spin and counts the rest itself.
///
/// Everything here is DISPLAY bookkeeping. The server still decides when the round ends —
/// freeSpinsRemaining hitting zero — and the balance is always its own.
/// </summary>
internal class FreeSpinRound
{

  /// <summary>Latched from the trigger spin's freeSpinsRemaining. The FreeSpinDisplay's "of N".</summary>
  internal int totalSpins;

  /// <summary>Derived each spin as totalSpins - freeSpinsRemaining.</summary>
  internal int spinsUsed;

  internal int multiplier = 1;

  /// <summary>
  /// The round's running total, for the bottom bar and the congratulations panel.
  ///
  /// SEEDED with the base spin that triggered the round, not with zero. That spin's line wins
  /// and the 1x total stake the trigger itself awards are part of what the player takes away
  /// from the feature, so the win field carries straight on from the number already on screen
  /// rather than blanking and starting again.
  /// </summary>
  internal double accumulatedWin;
  internal double triggerWin;
  internal double featureWins;



  internal static FreeSpinRound FromTrigger(int totalSpins, double triggerWin, int multiplier = 1)
  {
    return new FreeSpinRound
    {
      totalSpins = Mathf.Max(0, totalSpins),
      accumulatedWin = triggerWin,
      triggerWin = triggerWin,
      multiplier = Mathf.Max(1, multiplier)
    };
  }

  internal void AddSpins(int extraSpins)
  {
    if (extraSpins > 0)
    {
      totalSpins += extraSpins;
    }
  }

  /// <summary>
  /// Fold one completed free spin in. <paramref name="freeSpinsRemaining"/> is the server's
  /// own count, so the display can never drift from it.
  /// </summary>
  internal void RecordSpin(double winAmount, int freeSpinsRemaining)
  {
    accumulatedWin += winAmount;
    featureWins += winAmount;
    if (freeSpinsRemaining > totalSpins - spinsUsed)
    {
      totalSpins = spinsUsed + freeSpinsRemaining;
    }
    spinsUsed = Mathf.Clamp(totalSpins - freeSpinsRemaining, 0, totalSpins);
  }
}
