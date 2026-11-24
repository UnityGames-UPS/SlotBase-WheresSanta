﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#region Server Communication Models
[Serializable]
public class JackpotOpenRequest
{
  public string type = "JACKPOT_OPEN";
  public JackpotOpenPayload payload = new JackpotOpenPayload();
}

[Serializable]
public class JackpotOpenPayload
{
  public string tier;
}
[Serializable]
public class InitData
{
  public string id = "initData";
  public ServerGameData gameData;
  public ServerFeatures features;
  public ServerUIData uiData;
  public ServerPlayer player;
  public JackpotData jackpotData;
}
[Serializable]
public class JackpotData
{
  public JackpotValues values;

}

[Serializable]
public class JackpotValues
{
  public string miniJackpot;
  public string minorJackpot;
  public string majorJackpot;
  public string grandJackpot;
}

[Serializable]
public class JackpotSyncData
{
  public string gameId;
  public JackpotValues values;
}

[Serializable]
public class ServerGameData
{
  public List<List<int>> lines;
  public List<double> bets;

  public double creditDivisor = 0;

  public int totalLines;
}

/// <summary>
/// wheres Santa init "features" block.
/// </summary>
[Serializable]
public class ServerFeatures
{
  public Scatter scatter;
  public FreeGames freeGames;
  public WildSubstitution wildSubstitution;
  public ChristmasGiftsBonus christmasGiftsBonus;


  /// <summary>Minimum symbols on adjacent reels from the leftmost reel for a line to pay.</summary>
  public int minMatchCount;

}

/// <summary>
/// Reads a JSON field that may be a string, an array of strings, or null into a
/// <see cref="List{String}"/>. The server has shipped <c>activeFeature</c> as all three.
/// </summary>
public class StringOrArrayConverter : JsonConverter
{
  public override bool CanConvert(Type objectType) => objectType == typeof(List<string>);

  public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
                                  JsonSerializer serializer)
  {
    switch (reader.TokenType)
    {
      case JsonToken.Null:
        return null;
      case JsonToken.StartArray:
        return serializer.Deserialize<List<string>>(reader);
      default:
        var single = reader.Value?.ToString();
        return string.IsNullOrEmpty(single) ? new List<string>() : new List<string> { single };
    }
  }

  public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
  {
    serializer.Serialize(writer, value);
  }
}

[Serializable]
public class ServerUIData
{
  public PaylineData paylines;
}

[Serializable]
public class PaylineData
{
  public List<ServerSymbolInfo> symbols;
}

[Serializable]
public class ServerSymbolInfo
{
  public int id;
  public string name;
  public string description;

  /// <summary>
  /// Payout keyed by match count: { "3": 20, "4": 125, "5": 200 }. The jackpot symbols
  /// (ids 15-20) carry a single "1" key instead, since they pay on one symbol.
  /// Symbols with no payout at all (Wild, Mystery, the coins) send no key.
  /// </summary>
  public Dictionary<string, double> payout;
}


[Serializable]
public class ServerPlayer
{
  public double balance;
}

// ============================================================================
// FIXED: Server Response Models - Must match actual server JSON structure
// ============================================================================

[Serializable]
public class ServerSpinResponse
{
  public string id = "ResultData";
  public bool success;
  public List<List<string>> matrix; // Root level matrix sent by server
  public ServerPlayerBalance player;
  public ServerPayload payload;
  public ServerScatter scatter;
}

[Serializable]
public class ServerPlayerBalance
{
  public double? balance; // Nullable because server sends null
}

[Serializable]
public class ServerPayload
{
  public List<List<int>> matrix;
  public double winAmount;

  /// <summary>One entry per winning payline. Empty on a losing spin.</summary>
  public List<ServerLineWin> lineWins;
  [JsonProperty("paylineWins")]
  public List<ServerLineWin> paylineWins;
  public ChristmasGiftsData christmasGifts;
  public bool giftsBonusActive;
  public double baseLineWin;
  public double freeSpinMultiplierApplied;
  public double totalWin;
  public double gameWin;
  public double featureWin;
  public bool isFreeSpin;

  public int freeSpinsRemaining;

  public ServerScatter scatter;
}

[Serializable]
public class ChristmasGiftPosition
{
    public int row;
    public int col;
}

[Serializable]
public class ChristmasGiftTriggerLine
{
    public int lineIndex;
    public int triggerCount;
    public List<ChristmasGiftPosition> positions;
}

[Serializable]
public class ChristmasGiftsData
{
    public bool triggered;
    public int triggerCount;
    public int pickCount;
    public List<ChristmasGiftPosition> positions;
    public int lineIndex;
    public int giftCount;
    public List<ChristmasGiftTriggerLine> triggerLines;
}


[Serializable]
public class BonusRequest
{
    public string type = "BONUS";
    public BonusRequestPayload payload;
}

[Serializable]
public class BonusRequestPayload
{
    public string Event = "tap";
    public int index;
}

[Serializable]
public class BonusResultData
{
    public string id;
    public bool success;
    public ServerPlayerBalance player;
    public BonusResultPayload payload;
}

[Serializable]
public class BonusResultPayload
{
    public int giftIndex;
    public double prizeMultiplier;
    public double prizeWin;
    public int picksMade;
    public int pickCount;
    public double totalBonusWin;
    public List<int> selectedIndexes;
    public bool isOver;
    public double currentWinning;
}

[Serializable]
public class ServerScatter
{
  public bool triggered;
  public int scatterCount;
  public List<ChristmasGiftPosition> positions;
  public bool freeGamesTriggered;
  public int spinsAwarded;
  public double basePayout;
  public double winInCredits;
}


/// <summary>
/// One winning payline. <see cref="lineIndex"/> is a REAL index into
/// GameConfig.paylines — unlike the CNY ways-wins path, which produced a synthetic
/// incrementing id.
/// </summary>
[Serializable]
public class ServerLineWin
{
  public int lineIndex;
  public int symbolId;
  public string symbolName;
  public int matchCount;

  /// <summary>Paytable value in credits for this match count.</summary>
  public double payout;

  /// <summary>Cash won on this line (payout x bet).</summary>
  public double winAmount;

  // Backend sends winInCash and winInCredits in spinResult payload:
  public double winInCash;
  public double winInCredits;
  public double basePayout;

  /// <summary>
  /// Winning cells as "row,col" strings, ordered left-to-right from the leftmost reel:
  /// [ "0,0", "0,1", "0,2" ]. NOTE the row-first order — the client's resultMatrix is
  /// the other way round (column-major).
  /// </summary>
  public object positions;
  public object matchedPositions;
}


// ============================================================================
// Client-Side Spin Request
// ============================================================================

[Serializable]
public class SpinRequest
{
  public string type = "SPIN";
  public SpinPayload payload;
}

[Serializable]
public class SpinPayload
{
  public int betIndex;
  public bool isFreeSpin;
}




#endregion

#region Game Configuration (Client Side Converted)

[Serializable]
public class GameConfig
{
  public InitData initData;
  public Root rootData;
  public int reelCount = 5;
  public int rowCount = 3;


  public int symbolCount = 0;
  public int wildSymbolId = -1;
  public int scatterSymbolId = -1;
  public int bonusSymbolId = -1; // For Treasure Hunt Bonus

  public int paylineCount = 20;
  public List<List<int>> paylines;
  public List<double> availableBets;
  public List<SymbolInfo> symbols;

  // Minimum symbols on adjacent reels from the leftmost reel for a line to pay.
  public int minMatchCount = 3;

  public int betMultiplier = 1;      // CNY is cash-bet based, multiplier default is 1
  public double creditDivisor = 20;  // Credit divisor sent in initData
  public int maxWinMultiplier = 10000;
  public int minWinMultiplier = 10;
  public int initialFreeSpins = 10;
  // public ExtraSpinsData extraSpinsData; // Keep to avoid compilation error in UI

  /// <summary>
  /// The raw init "features" block — pig meter configuration plus the live meter state.
  /// Carried verbatim; the meters / jackpot / Mystery presentation that reads it is still
  /// to be built.
  /// </summary>
  public ServerFeatures features;
}

[Serializable]
public class SymbolInfo
{
  public int id;
  public string name;

  /// <summary>
  /// Server-authored blurb. Only the non-paying symbols (Wild, Mystery, the coins) ship
  /// one; the paying symbols send an empty string and show their payout rows instead.
  /// </summary>
  public string description;

  /// <summary>Payouts ordered by match count DESCENDING — index 0 is the longest run.</summary>
  public List<double> multipliers;

  /// <summary>
  /// Match count for each entry in <see cref="multipliers"/>, same order. Usually 5/4/3,
  /// but the jackpot symbols pay on a single symbol so theirs is just { 1 }.
  /// </summary>
  public List<int> matchCounts;

  public bool isWild;
  public bool isScatter;
  public int wildMultiplier = 1;
  public int minMatch;
}

#endregion

#region Player & Game State (Client Side)

[Serializable]
public class PlayerData
{
  public double balance;
  public int currentBetIndex;
}

[Serializable]
public class SpinResult
{
  public List<List<int>> resultMatrix;  // Client uses int matrix
  public double winAmount;
  public double grandTotalWin;
  public List<WinLine> winLines;

  public PlayerData playerData;
  public ServerScatter scatter;


  // Server-authoritative free spin state
  public int serverSpinsRemaining;
  
  
  public bool isRoundOver;

  public double baseLineWin;
  public double gameWin;
  public double featureWin;
  public double totalWin;

  public bool giftsBonusActive;
  public ChristmasGiftsData christmasGifts;

}

[Serializable]
public class WinLine
{
  public int lineId;
  public int symbolId;
  public List<int> positions;  // Flat list: [row * 5 + col]
  public double winAmount;
}


#endregion

#region Platform Communication

[Serializable]
public class AuthData
{
  public string token;
  public string socketURL;
  public string nameSpace;
}

#endregion

#region Enums

public enum GameState
{
  Initializing,
  Idle,
  Spinning,
  Stopping,
  ShowingWin,
  FreeSpinMode
}

public enum SpinSpeed
{
  Normal,
  Turbo,
  QuickSpin
}

#endregion

#region Helper Classes for Conversion

/// <summary>
/// Converts server data to client GameConfig
/// </summary>
public static class InitDataConverter
{
  internal static GameConfig ConvertToGameConfig(InitData serverData)
  {
    int lineCount = (serverData.gameData?.lines != null && serverData.gameData.lines.Count > 0)
        ? serverData.gameData.lines.Count
        : (serverData.gameData != null && serverData.gameData.totalLines > 0 ? serverData.gameData.totalLines : 20);

    var config = new GameConfig
    {
      reelCount = DeriveReelCount(serverData.gameData),
      rowCount = DeriveRowCount(serverData.gameData),
      symbolCount = serverData.uiData?.paylines?.symbols != null ? serverData.uiData.paylines.symbols.Count : 0,
      paylineCount = lineCount,
      paylines = serverData.gameData?.lines,
      availableBets = serverData.gameData?.bets,
      creditDivisor = (serverData.gameData != null && serverData.gameData.creditDivisor > 0) ? serverData.gameData.creditDivisor : lineCount,
      symbols = new List<SymbolInfo>(),
      initData = serverData
    };

    if (serverData.features?.wildSubstitution?.wildSymbolIds?.Count > 0)
    {
      config.wildSymbolId = serverData.features.wildSubstitution.wildSymbolIds[0];
    }

    if (serverData.features?.scatter != null && serverData.features.scatter.scatterSymbolId > 0)
    {
      config.scatterSymbolId = serverData.features.scatter.scatterSymbolId;
    }

    if (serverData.features?.christmasGiftsBonus != null && serverData.features.christmasGiftsBonus.bonusSymbolId >= 0)
    {
      config.bonusSymbolId = serverData.features.christmasGiftsBonus.bonusSymbolId;
    }

    if (serverData.uiData?.paylines?.symbols != null)
    {
      foreach (var serverSymbol in serverData.uiData.paylines.symbols)
      {
        string nameLower = (serverSymbol.name ?? "").ToLower();

        bool isWild = (serverData.features?.wildSubstitution?.wildSymbolIds != null &&
                       serverData.features.wildSubstitution.wildSymbolIds.Contains(serverSymbol.id))
                      || nameLower.Contains("wild");

        bool isScatter = (serverData.features?.scatter != null &&
                          serverData.features.scatter.scatterSymbolId == serverSymbol.id)
                         || nameLower.Contains("scatter");

        if (isWild && config.wildSymbolId == -1) config.wildSymbolId = serverSymbol.id;
        if (isScatter && config.scatterSymbolId == -1) config.scatterSymbolId = serverSymbol.id;

        var symbolInfo = new SymbolInfo
        {
          id = serverSymbol.id,
          name = serverSymbol.name,
          description = serverSymbol.description,
          multipliers = new List<double>(),
          matchCounts = new List<int>(),
          isWild = isWild,
          isScatter = isScatter
        };

        // payout arrives keyed by match count — { "3": 20, "4": 125, "5": 200 }
        if (serverSymbol.payout != null && serverSymbol.payout.Count > 0)
        {
          var entries = new List<KeyValuePair<int, double>>();
          foreach (var kv in serverSymbol.payout)
          {
            if (int.TryParse(kv.Key, out int matchCount))
              entries.Add(new KeyValuePair<int, double>(matchCount, kv.Value));
            else
              UnityEngine.Debug.LogError($"[InitDataConverter] Symbol '{serverSymbol.name}' has a non-numeric payout key '{kv.Key}'.");
          }
          entries.Sort((a, b) => b.Key.CompareTo(a.Key));

          foreach (var entry in entries)
          {
            symbolInfo.matchCounts.Add(entry.Key);
            symbolInfo.multipliers.Add(entry.Value);
          }

          if (entries.Count > 0) symbolInfo.minMatch = entries[entries.Count - 1].Key;
        }
        else
        {
          var defaultPayout = GetWheresSantaDefaultPayout(serverSymbol.id);
          if (defaultPayout != null)
          {
            foreach (var entry in defaultPayout)
            {
              symbolInfo.matchCounts.Add(entry.Key);
              symbolInfo.multipliers.Add(entry.Value);
            }
            if (symbolInfo.matchCounts.Count > 0)
              symbolInfo.minMatch = symbolInfo.matchCounts[symbolInfo.matchCounts.Count - 1];
          }
        }
        config.symbols.Add(symbolInfo);
      }
    }

    if (serverData.features != null)
    {
      if (serverData.features.minMatchCount > 0)
        config.minMatchCount = serverData.features.minMatchCount;

      config.features = serverData.features;
    }

    return config;
  }

  public static List<KeyValuePair<int, double>> GetWheresSantaDefaultPayout(int symbolId)
  {
    switch (symbolId)
    {
      case 1: // House (Scatter)
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 500),
          new KeyValuePair<int, double>(4, 50),
          new KeyValuePair<int, double>(3, 5),
          new KeyValuePair<int, double>(2, 2)
        };
      case 3: // Golden Bells
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 500),
          new KeyValuePair<int, double>(4, 150),
          new KeyValuePair<int, double>(3, 40),
          new KeyValuePair<int, double>(2, 2)
        };
      case 4: // Christmas Stocking
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 400),
          new KeyValuePair<int, double>(4, 100),
          new KeyValuePair<int, double>(3, 25),
          new KeyValuePair<int, double>(2, 2)
        };
      case 5: // Green Gift
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 200),
          new KeyValuePair<int, double>(4, 70),
          new KeyValuePair<int, double>(3, 20)
        };
      case 6: // Ace
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 150),
          new KeyValuePair<int, double>(4, 50),
          new KeyValuePair<int, double>(3, 15)
        };
      case 7: // King
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 125),
          new KeyValuePair<int, double>(4, 40),
          new KeyValuePair<int, double>(3, 10)
        };
      case 8: // Queen
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 100),
          new KeyValuePair<int, double>(4, 30),
          new KeyValuePair<int, double>(3, 10)
        };
      case 9: // Jack
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 75),
          new KeyValuePair<int, double>(4, 20),
          new KeyValuePair<int, double>(3, 5)
        };
      case 10: // Ten
        return new List<KeyValuePair<int, double>>
        {
          new KeyValuePair<int, double>(5, 50),
          new KeyValuePair<int, double>(4, 15),
          new KeyValuePair<int, double>(3, 5)
        };
      default:
        return null;
    }
  }


  /// <summary>Columns: the width of a payline, else 5.</summary>
  private static int DeriveReelCount(ServerGameData gameData)
  {
    if (gameData?.lines != null && gameData.lines.Count > 0 && gameData.lines[0] != null)
      return gameData.lines[0].Count;
    return 5;
  }

  /// <summary>
  /// Rows: one past the highest row index any payline touches — 3 for Rich Piggies, whose
  /// lines only ever name rows 0/1/2. Replaces the old
  /// "totalLines == 243 ? 3 : 1024 ? 4 : 3" guess, which returned 3 for a 25-line game
  /// only by falling through to its default branch.
  /// </summary>
  private static int DeriveRowCount(ServerGameData gameData)
  {
    int maxRow = -1;
    if (gameData?.lines != null)
    {
      foreach (var line in gameData.lines)
      {
        if (line == null) continue;
        foreach (int row in line) if (row > maxRow) maxRow = row;
      }
    }
    return maxRow >= 0 ? maxRow + 1 : 3;
  }

  internal static PlayerData ConvertToPlayerData(ServerPlayer serverPlayer, int defaultBetIndex = 0)
  {
    return new PlayerData
    {
      // balance = serverPlayer.balance,
      balance = serverPlayer != null ? serverPlayer.balance : 0,
      currentBetIndex = defaultBetIndex
    };
  }

  /// <summary>
  /// Converts server response to client SpinResult
  /// </summary>
  internal static SpinResult ConvertServerResponseToSpinResult(ServerSpinResponse serverResponse, double currentBalance, double betAmount, GameConfig gameConfig)
  {
    var payload = serverResponse.payload;

    double winAmountVal = payload != null ? (payload.totalWin > 0 ? payload.totalWin : payload.winAmount) : 0;
    // double totalPay = (gameConfig != null && gameConfig.creditDivisor > 0) ? betAmount * gameConfig.creditDivisor : betAmount * 25;
    double totalPay = (gameConfig != null && gameConfig.creditDivisor > 0)
        ? betAmount * gameConfig.creditDivisor
        : betAmount * (gameConfig != null && gameConfig.paylineCount > 0 ? gameConfig.paylineCount : 20);
    double newBalance = serverResponse.player?.balance ?? CalculateNewBalance(currentBalance, totalPay, winAmountVal);

    // int spinsRemaining = payload != null ? payload.freeSpinsRemaining : 0;
    // bool inFeature = payload?.activeFeature != null && payload.activeFeature.Count > 0;
    var scatterObj = payload?.scatter ?? serverResponse.scatter;
    int spinsRemaining = payload?.freeSpinsRemaining ?? 0;
    if (spinsRemaining == 0 && scatterObj != null && scatterObj.freeGamesTriggered)
    {
      spinsRemaining = scatterObj.spinsAwarded;
    }

    var lines = ConvertLineWins((payload?.paylineWins != null && payload.paylineWins.Count > 0)
        ? payload.paylineWins
        : payload?.lineWins, gameConfig);
    if (scatterObj != null && scatterObj.positions != null && scatterObj.positions.Count > 0 && (scatterObj.winInCredits > 0 || scatterObj.basePayout > 0))
    {
      int reels = gameConfig != null && gameConfig.reelCount > 0 ? gameConfig.reelCount : 5;
      var pos = new List<int>();
      foreach (var p in scatterObj.positions) if (p != null) pos.Add(p.row * reels + p.col);
      int symId = gameConfig != null && gameConfig.scatterSymbolId > 0 ? gameConfig.scatterSymbolId : 1;
      lines.Add(new WinLine { lineId = -1, symbolId = symId, positions = pos, winAmount = scatterObj.winInCredits > 0 ? scatterObj.winInCredits : scatterObj.basePayout });
    }

    var result = new SpinResult
    {
      resultMatrix = ConvertReelsToMatrix(serverResponse.payload?.matrix, serverResponse.matrix, gameConfig),
      winAmount = winAmountVal,
      grandTotalWin = winAmountVal,
      winLines = lines,

      playerData = new PlayerData
      {
        balance = newBalance,
        currentBetIndex = 0
      },

      scatter = scatterObj,

      serverSpinsRemaining = spinsRemaining,
      isRoundOver = spinsRemaining <= 0,

      baseLineWin = payload != null ? payload.baseLineWin : 0,
      gameWin = payload != null ? payload.gameWin : 0,
      featureWin = payload != null ? payload.featureWin : 0,
      totalWin = payload != null ? payload.totalWin : 0,

      giftsBonusActive = payload != null && payload.giftsBonusActive,
      christmasGifts = payload?.christmasGifts
    };

    return result;
  }

  /// <summary>
  /// Flatten payload.lineWins into the client WinLine model.
  ///
  /// Server positions are "row,col" strings ordered left-to-right from the leftmost reel:
  /// [ "0,0", "0,1", "0,2" ]. The client encodes a cell as a row-major flat index,
  /// <c>flat = row * reelCount + col</c>, which SlotView.DecodeFlatIndex inverts.
  /// (SpinResult.resultMatrix is the other way round — column-major.)
  ///
  /// Order is preserved, so positions[0] is always the leftmost reel of the run.
  /// </summary>
  private static List<WinLine> ConvertLineWins(List<ServerLineWin> serverLineWins,GameConfig gameConfig)
  {
      var winLines = new List<WinLine>();

      if (serverLineWins == null)
          return winLines;

      int reelCount = gameConfig != null && gameConfig.reelCount > 0 ? gameConfig.reelCount : 5;
      int rowCount = gameConfig != null && gameConfig.rowCount > 0 ? gameConfig.rowCount : 3;

      foreach (var serverWin in serverLineWins)
      {
          if (serverWin == null)
              continue;

          var positions = new List<int>();

          var rawPositions = serverWin.matchedPositions ?? serverWin.positions;

          if (rawPositions is System.Collections.IEnumerable enumerable)
          {
              foreach (var item in enumerable)
              {
                  string cell = item.ToString();

                  if (item is Newtonsoft.Json.Linq.JObject obj && obj["row"] != null && obj["col"] != null)
                      cell = $"{obj["row"]},{obj["col"]}";
                  else if (item is Newtonsoft.Json.Linq.JArray arr && arr.Count >= 2)
                      cell = $"{arr[0]},{arr[1]}";

                  if (TryParseCell(cell, reelCount, rowCount, out int flatIndex))
                      positions.Add(flatIndex);
              }
          }

          if (serverWin.matchCount > 0 && positions.Count > serverWin.matchCount)
              positions = positions.GetRange(0, serverWin.matchCount);

          if (positions.Count == 0)
              continue;

          winLines.Add(new WinLine
          {
              lineId = serverWin.lineIndex,
              symbolId = serverWin.symbolId,
              positions = positions,
              // Read line win from winInCash (preferred), winAmount, winInCredits, or payout:
              winAmount = serverWin.winInCash > 0 ? serverWin.winInCash
                        : serverWin.winAmount > 0 ? serverWin.winAmount
                        : serverWin.winInCredits > 0 ? serverWin.winInCredits
                        : serverWin.payout
          });
      }

      return winLines;
  }

  /// <summary>Parse a "row,col" cell into a row-major flat index. False on anything malformed.</summary>
  private static bool TryParseCell(string cell, int reelCount, int rowCount, out int flatIndex)
  {
    flatIndex = -1;
    if (string.IsNullOrEmpty(cell)) return false;

    int comma = cell.IndexOf(',');
    if (comma <= 0 || comma >= cell.Length - 1) return false;

    if (!int.TryParse(cell.Substring(0, comma).Trim(), out int row)) return false;
    if (!int.TryParse(cell.Substring(comma + 1).Trim(), out int col)) return false;

    if (row < 0 || row >= rowCount || col < 0 || col >= reelCount) return false;

    flatIndex = row * reelCount + col;
    return true;
  }

  private static List<List<int>> ConvertReelsToMatrix(List<List<int>> payloadMatrix, List<List<string>> rootMatrix, GameConfig gameConfig)
  {
    int rowCount = gameConfig != null ? gameConfig.rowCount : 3;
    if (payloadMatrix != null && payloadMatrix.Count > 0)
    {
      int totalRows = payloadMatrix.Count;
      int totalCols = payloadMatrix[0].Count;
      var matrix = new List<List<int>>();

      for (int col = 0; col < totalCols; col++)
      {
        var column = new List<int>();
        for (int row = 0; row < totalRows; row++)
        {
          column.Add(payloadMatrix[row][col]);
        }
        matrix.Add(column);
      }
      return matrix;
    }
    if (rootMatrix != null && rootMatrix.Count > 0)
    {
      int totalRows = rootMatrix.Count;
      int totalCols = rootMatrix[0].Count;
      var matrix = new List<List<int>>();

      for (int col = 0; col < totalCols; col++)
      {
        var column = new List<int>();
        for (int row = 0; row < totalRows; row++)
        {
          int.TryParse(rootMatrix[row][col], out int id);
          column.Add(id);
        }
        matrix.Add(column);
      }
      return matrix;
    }

    UnityEngine.Debug.LogError("Invalid server reels/matrix sourceReels is null or empty");
    return GenerateDefaultMatrix(rowCount);
  }
  private static List<List<int>> GenerateDefaultMatrix(int rowCount)
  {
    var matrix = new List<List<int>>();
    for (int col = 0; col < 5; col++)
    {
      var column = new List<int>();
      for (int row = 0; row < rowCount; row++)
      {
        column.Add(0);
      }
      matrix.Add(column);
    }
    return matrix;
  }

  private static double CalculateNewBalance(double currentBalance, double totalPay, double winAmount)
  {
    return currentBalance + winAmount;
  }
}

#endregion
