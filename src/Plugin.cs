// ---------------------------------------------------------------
//  Kartentuer hacken
//
//  Haengt den Eintrag "Hacken" ins Menue jeder verschlossenen
//  Kartentuer (Labs, Reserve, Interchange-Kiba usw.). Nach einer
//  kurzen Hackzeit geht die Tuer auf - ohne Zugangskarte.
//
//  WO DER HEBEL SITZT: Das Menue fuer Kartentueren baut
//  EFT.InteractionContextHelper.GetAvailableActions(
//      GamePlayerOwner, KeycardDoor, bool isProxy).
//  isProxy = true heisst: du schaust auf den Kartenleser. Dann
//  listet das Spiel deine Karten auf ("Karte versuchen ...") oder
//  meldet "Keine Karten". Schaust du auf die Tuer selbst, steht
//  nur "Verschlossen" da. In beiden Faellen haengen wir "Hacken"
//  an und loeschen den Fehlertext.
//
//  WIE GEOEFFNET WIRD: door.Unlock() - dieselbe Methode, die auch
//  eine passende Karte am Ende ausloest. KeycardDoor ueberschreibt
//  UnlockCoroutine: Leser-Griff, Zustand Shut, gruener Piepton,
//  und bei Tueren mit _openOnUnlock schwingt sie gleich auf.
//  Bots bekommen es ueber den DoorState-Setter mit.
//
//  Einstellbar unter F12 -> Kartentuer hacken.
// ---------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.Communications;
using EFT.Interactive;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace KartentuerHacken
{
	[BepInPlugin("com.kaan.kartentuerhacken", "Kartentuer hacken", "1.0.0")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Plugin Instanz;

		internal static ManualLogSource Log;

		internal static ConfigEntry<bool> An;

		internal static ConfigEntry<bool> KarteAutomatisch;

		internal static ConfigEntry<float> Hackdauer;

		internal static ConfigEntry<float> MaxAbstand;

		internal static ConfigEntry<int> Erfolgschance;

		internal static ConfigEntry<string> Beschriftung;

		internal static ConfigEntry<bool> Protokoll;

		// Tueren, an denen gerade gehackt wird - kein zweiter Versuch
		// parallel, falls man den Eintrag mehrfach anklickt.
		internal static readonly HashSet<KeycardDoor> Laufend = new HashSet<KeycardDoor>();

		private void Awake()
		{
			Instanz = this;
			Log = base.Logger;

			An = base.Config.Bind("1. Allgemein", "Eingeschaltet", defaultValue: true,
				"Zeigt \"Hacken\" an jeder verschlossenen Kartentuer.");

			KarteAutomatisch = base.Config.Bind("1. Allgemein", "Passende Karte automatisch waehlen", defaultValue: true,
				"An: Am Kartenleser steht statt \"Karte X versuchen\" fuer jede Karte nur noch EIN Eintrag \"Aufschliessen\" mit der passenden Karte " +
				"(dieselbe Schluesselsuche wie bei normalen Tueren). Falsche Karten werden nicht mehr angeboten. Aus: Verhalten wie ab Werk.");

			Hackdauer = base.Config.Bind("1. Allgemein", "Hackdauer (Sekunden)", 5f,
				new ConfigDescription("So lange dauert das Hacken. 0 = sofort offen.",
					new AcceptableValueRange<float>(0f, 60f)));

			MaxAbstand = base.Config.Bind("1. Allgemein", "Max. Abstand (Meter)", 3f,
				new ConfigDescription("Entfernst du dich waehrend des Hackens weiter als das vom Startpunkt, bricht es ab.",
					new AcceptableValueRange<float>(1f, 20f)));

			Erfolgschance = base.Config.Bind("1. Allgemein", "Erfolgschance (%)", 100,
				new ConfigDescription("Unter 100 kann das Hacken fehlschlagen (roter Piepton), dann einfach nochmal.",
					new AcceptableValueRange<int>(1, 100)));

			Beschriftung = base.Config.Bind("2. Anzeige", "Beschriftung", "Hacken",
				"Text des Menueeintrags.");

			Protokoll = base.Config.Bind("3. Diagnose", "Protokoll", defaultValue: false,
				"Schreibt jeden Hackversuch in die BepInEx-Konsole.");

			try
			{
				new Harmony("com.kaan.kartentuerhacken").PatchAll();
				Log.LogInfo("Kartentuer hacken: bereit.");
			}
			catch (Exception ex)
			{
				Log.LogError("Kartentuer hacken: Patch fehlgeschlagen - " + ex);
			}
		}
	}

	[HarmonyPatch]
	internal static class KartentuerMenuePatch
	{
		// Ueberladung vom Compiler aussuchen lassen - aendert ein
		// SPT-Update die Signatur, scheitert schon der Bau.
		[HarmonyTargetMethod]
		internal static MethodBase Ziel()
		{
			Func<GamePlayerOwner, KeycardDoor, bool, AvailableInteractionState> zeiger =
				InteractionContextHelper.GetAvailableActions;
			return zeiger.Method;
		}

		[HarmonyPostfix]
		internal static void Nachher(GamePlayerOwner owner, KeycardDoor door, bool isProxy, AvailableInteractionState __result)
		{
			try
			{
				if (!Plugin.An.Value || __result == null || __result.Actions == null)
				{
					return;
				}
				if ((object)door == null || (object)owner == null || (object)owner.Player == null)
				{
					return;
				}
				if (door.DoorState != EDoorState.Locked)
				{
					return;
				}

				// Passende Karte automatisch: Das Spiel listet am Kartenleser
				// (isProxy) jede besessene Karte als "Karte X versuchen" - auch
				// die falschen. Normale Tueren waehlen den passenden Schluessel
				// dagegen selbst (InteractionContextHelper.TryAddUnlock ueber
				// owner.GetKey). Genau diese Spielfunktion nutzen wir hier; im
				// gesperrten Zustand stehen bis hierher nur die Karteneintraege
				// des Spiels in der Liste.
				if (isProxy && Plugin.KarteAutomatisch.Value)
				{
					var passend = new List<InteractionAction>();
					var ergebnis = InteractionContextHelper.TryAddUnlock(owner, door, passend);
					__result.Actions.Clear();
					__result.Actions.AddRange(passend);
					if (ergebnis.Failed && Plugin.Protokoll.Value)
					{
						Plugin.Log.LogInfo("Kartentuer: keine passende Karte fuer " + door.Id
							+ (string.IsNullOrEmpty(ergebnis.Error) ? "" : " (" + ergebnis.Error + ")"));
					}
				}

				string text = Plugin.Beschriftung.Value;
				for (int i = 0; i < __result.Actions.Count; i++)
				{
					InteractionAction eintrag = __result.Actions[i];
					if (eintrag != null && eintrag.Name == text)
					{
						return;
					}
				}

				KeycardDoor tuer = door;
				GamePlayerOwner besitzer = owner;

				__result.Actions.Add(new InteractionAction
				{
					Name = text,
					Disabled = !door.Operatable || Plugin.Laufend.Contains(door),
					Action = delegate
					{
						Starten(besitzer, tuer);
					}
				});

				// Solange ein Fehlertext ("Keine Karten", "Verschlossen")
				// gesetzt ist, zeigt das Spiel das Menue grau an.
				__result.Error = null;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Kartentuer hacken: Menueaufbau - " + ex);
			}
		}

		private static void Starten(GamePlayerOwner owner, KeycardDoor door)
		{
			try
			{
				// Sonst bleibt die Spielfigur im Bedien-Zustand haengen.
				owner.Player.MovementContext.ResetCanUsePropState();

				if (Plugin.Laufend.Contains(door) || door.DoorState != EDoorState.Locked)
				{
					return;
				}
				Plugin.Laufend.Add(door);
				Plugin.Instanz.StartCoroutine(Hacken(owner.Player, door));
			}
			catch (Exception ex)
			{
				Plugin.Laufend.Remove(door);
				Plugin.Log.LogError("Kartentuer hacken: Start - " + ex);
			}
		}

		private static IEnumerator Hacken(Player player, KeycardDoor door)
		{
			float dauer = Plugin.Hackdauer.Value;
			if (dauer > 0f)
			{
				Meldung("Kartenleser wird gehackt ... (" + dauer.ToString("0.#") + " s)");
			}

			Vector3 start = player.Position;
			float ende = Time.time + dauer;
			while (Time.time < ende)
			{
				if (!NochDa(player, door, start))
				{
					Plugin.Laufend.Remove(door);
					Meldung("Hacken abgebrochen - zu weit weg.");
					yield break;
				}
				yield return null;
			}

			try
			{
				if (!NochDa(player, door, start) || door.DoorState != EDoorState.Locked)
				{
					yield break;
				}

				bool erfolg = UnityEngine.Random.Range(0, 100) < Plugin.Erfolgschance.Value;
				if (erfolg)
				{
					door.Unlock();
					Meldung("Zugang gewaehrt.");
				}
				else
				{
					door.StartCoroutine(door.PlayResultSound(success: false));
					Meldung("Hacken fehlgeschlagen - nochmal versuchen.");
				}

				if (Plugin.Protokoll.Value)
				{
					Plugin.Log.LogInfo("Hackversuch an " + door.Id + " (Karte " + door.KeyId + "): "
						+ (erfolg ? "offen" : "fehlgeschlagen"));
				}
			}
			finally
			{
				Plugin.Laufend.Remove(door);
			}
		}

		private static bool NochDa(Player player, KeycardDoor door, Vector3 start)
		{
			if ((object)player == null || player == null || !player.HealthController.IsAlive)
			{
				return false;
			}
			if (door == null)
			{
				return false;
			}
			return Vector3.Distance(player.Position, start) <= Plugin.MaxAbstand.Value;
		}

		private static void Meldung(string text)
		{
			try
			{
				NotificationManager.DisplayMessageNotification(text);
			}
			catch (Exception)
			{
				// Ohne Meldung geht es auch.
			}
		}
	}
}
