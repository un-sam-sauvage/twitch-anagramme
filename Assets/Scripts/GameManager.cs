using System.Linq;
using TwitchChatConnect.Client;
using TwitchChatConnect.Data;
using TwitchChatConnect.Manager;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Unity.VisualScripting;

public class GameManager : MonoBehaviour
{	
	[SerializeField]
	private GameObject letterContainer;
	[SerializeField]
	private GameObject letterPrefab;
	[SerializeField]
	private GameObject wordsContainer;
	[SerializeField]
	private GameObject wordContainerPrefab;
	[SerializeField]
	private GameObject letterPlaceholderPrefab;
	[SerializeField]
	private GameObject scoreContainer;
	[SerializeField]
	private GameObject betweenRoundsPanel;

	[SerializeField]
	private Image timerImage;

	[SerializeField]
	private float originalTimer;

	[SerializeField]
	private GameObject particleWordFound;

	private List<Player> players = new List<Player>();

	private List<string> wordsFound = new List<string>();

	private bool isPlaying = true;
	private bool isDisplayingLetter = false;

	private int currentId = -1;

	private float currentTimer;

	private string[] words;
	// Start is called before the first frame update
	void Start()
	{
		StartGame();
		TwitchChatClient.instance.Init(() =>
			{
				// Debug.Log("Connected!");
				TwitchChatClient.instance.onChatMessageReceived += ShowMessage;

				TwitchUserManager.OnUserAdded += twitchUser =>
				{
					// Debug.Log($"{twitchUser.Username} has connected to the chat.");
				};

				TwitchUserManager.OnUserRemoved += username =>
				{
					// Debug.Log($"{username} has left the chat.");
				};
			},
			message =>
			{
				// Error when initializing.
				Debug.LogError(message);
			}
		);
	}

	private void Update() {
		if (isPlaying) {
			currentTimer -= Time.deltaTime;
			timerImage.fillAmount = currentTimer / originalTimer;
			timerImage.gameObject.GetComponentInChildren<TMP_Text>().text = Math.Round(currentTimer).ToString();
			if (currentTimer <= 0) {
				isPlaying = false;
				ResetBoard();
			}
			if (words.Length == wordsFound.Count && !isDisplayingLetter) {
				ResetBoard();
			}
		}
	}

	private void ShowMessage(TwitchChatMessage chatMessage)
	{
		string message = chatMessage.Message;
		if (isPlaying) {
			if (message.IndexOf(" ") < 0) {
				message = message.ToUpper();
				if (words.Contains(message) && !wordsFound.Contains(message)) {
					WordFound(message, chatMessage.User.DisplayName);
				}
			}
		}
	}


	//Delete everything on the game board to prepare the next round
	private void ResetBoard () {
		particleWordFound.transform.SetParent(null);

		//Delete all letter placeholder
		while (wordsContainer.transform.childCount > 0) {
			DestroyImmediate(wordsContainer.transform.GetChild(0).gameObject);
		}
		while (letterContainer.transform.childCount > 0) {
			DestroyImmediate(letterContainer.transform.GetChild(0).gameObject);
		}
		wordsFound = new List<string>();
		StartCoroutine("BetweenRounds");
	}

	//TODO: faire l'animation d'entre rounds (en mode rappel des scores)
	// Taille des fonts à faire pour le podium
	//36 28 20 15 15...
	private IEnumerator BetweenRounds() {
		isPlaying = false;
		betweenRoundsPanel.SetActive(true);
		//Reset score board
		while (betweenRoundsPanel.transform.childCount > 0) {
			DestroyImmediate(betweenRoundsPanel.transform.GetChild(0).gameObject);
		}

		//TODO: c'est censé trier la liste par odre croissant qu'on reverse après pour l'ordre décroissant. A tester avec d'autres comptes
		players.Sort((p1,p2)=>p1.score.CompareTo(p2.score));
		players.Reverse();

		foreach (Player player in players) {
			GameObject scoreRappel = Instantiate(letterPrefab, betweenRoundsPanel.transform.position, Quaternion.identity);
			scoreRappel.GetComponent<TMP_Text>().text = $"{player.username} : {player.score}";
			scoreRappel.transform.SetParent(betweenRoundsPanel.transform, false);
			scoreRappel.GetComponent<TMP_Text>().fontSize = players.IndexOf(player) + 1 switch
			{
				1 => 36,
				2 => 28,
				3 => 20,
				_ => (float)15,
			};
		}

		yield return new WaitForSeconds(5);
		isPlaying = true;
		betweenRoundsPanel.SetActive(false);
		StartGame();
	}

	private IEnumerator AnimationDisplayWordFound (string word, GameObject wordContainer) {
		foreach (char letterInWord in word) {

			//on le met à true dans le forEach pour ne pas qu'une autre animation puisse le passer à false alors qu'il reste encore une animation en cours, dans le cas où il y en aurait 2 en même temps
			isDisplayingLetter = true;
			GameObject letter = Instantiate(letterPrefab, letterContainer.transform.position, Quaternion.identity);
			letter.GetComponent<TMP_Text>().text = letterInWord.ToString();
			if (!wordContainer) {
				yield return new WaitForSeconds(0);
			} else {
				letter.transform.SetParent(wordContainer.transform, false);
				letter.transform.localScale = Vector3.zero;
				particleWordFound.transform.SetParent(letter.transform);
				particleWordFound.transform.localScale = Vector3.one;
				particleWordFound.GetComponent<ParticleSystem>().Play();
				letter.transform.DOScale(new Vector3(1,1,1), .25f);
			}
			yield return new WaitForSeconds(.25f);
		}
		isDisplayingLetter = false;
	}

	//Allow to launch game
	private void StartGame() {

		currentTimer = originalTimer;

		string jsonPath = Application.streamingAssetsPath + "/words.json";
		string jsonStr = File.ReadAllText(jsonPath);

		GameContainer gameContainer = JsonUtility.FromJson<GameContainer>(jsonStr);
		Words randomGame = gameContainer.gameContainer[UnityEngine.Random.Range(0, gameContainer.gameContainer.Length)];
		if (randomGame.id == currentId) {
			while (randomGame.id == currentId) {
				randomGame = gameContainer.gameContainer[UnityEngine.Random.Range(0, gameContainer.gameContainer.Length)];
			}
		}
		currentId = randomGame.id;

		string[] letters = randomGame.letters;
		words = randomGame.words;
		
		DisplayLetter(letters);
		DisplayWords(words);
	}

	//as it says
	private void DisplayLetter(string[] letters) {
		foreach (string letter in letters) {
			GameObject newLetterPrefab =  Instantiate(letterPrefab, letterContainer.transform.position, Quaternion.identity);
			newLetterPrefab.GetComponent<TMP_Text>().text = letter;
			newLetterPrefab.transform.SetParent(letterContainer.transform, false);
		}
	}

	//as it says
	private void DisplayWords(string[] words) {
		foreach (string word in words) {
			GameObject wordContainer = Instantiate(wordContainerPrefab, wordsContainer.transform.position, Quaternion.identity);
			wordContainer.transform.SetParent(wordsContainer.transform, false);
			foreach (char letter in word) {
				GameObject letterPlaceholder = Instantiate(letterPlaceholderPrefab, wordContainer.transform.position, Quaternion.identity);
				letterPlaceholder.transform.SetParent(wordContainer.transform, false);
			}
		}
	}

	//handle a correct response with score management
	//FIXME: peut être un bug, quand j'ai tapé mon score une fois j'ai vu qu'il n'était pas pris en compte ou réinitialisé...
	//A voir si c'est parce qu'il y avait d'autres scores faire des tests avec plusieurs personnes
	private void WordFound (string word, string playerName) {
		GameObject[] wordContainers = GameObject.FindGameObjectsWithTag("WordContainer");
		foreach (GameObject wordContainer in wordContainers) {
			if (wordContainer.transform.childCount == word.Length) {

				//Delete all letter placeholder
				while (wordContainer.transform.childCount > 0) {
					DestroyImmediate(wordContainer.transform.GetChild(0).gameObject);
				}
				StartCoroutine(AnimationDisplayWordFound(word, wordContainer));
				wordContainer.tag = "Untagged";

				//Set du score ici
				GameObject usernameScore = GameObject.Find(playerName);
				if (usernameScore == null) {

					Player newPlayer = new Player()
					{
						username = playerName,
						score = word.Length
					};
					players.Add(newPlayer);

					usernameScore = Instantiate(letterPrefab, scoreContainer.transform.position, Quaternion.identity);
					usernameScore.GetComponent<TMP_Text>().text = $"{playerName} : {newPlayer.score}";
					usernameScore.GetComponent<TMP_Text>().fontSize = 20;
					usernameScore.transform.SetParent(scoreContainer.transform, false);
					usernameScore.name = playerName;
				} else {
					foreach (Player player in players) {
						if (player.username == playerName) {
							player.score += word.Length;
							usernameScore.GetComponent<TMP_Text>().text = $"{playerName} : {player.score}";
							break;
						}
					}
				}
				wordsFound.Add(word);
				break;
			}
		}
	}
}

[Serializable]
public class GameContainer {
	public Words[] gameContainer;
}

[Serializable]
public class Words {
	public int id;
	public string[] letters;
	public string[] words;
}

[Serializable]
public class Player {
	public string username;
	public int score;
}