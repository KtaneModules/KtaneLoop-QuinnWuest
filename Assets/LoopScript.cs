﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class LoopScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable[] ArrowSels;
    public Texture[] ColorTextures;
    public GameObject[] ArrowObjs;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private int _loopId;
    private static int _loopIdCounter = 1;
    private bool _moduleSolved;

    private const string primary = "_MainTex";
    private const string secondary = "_SecondTex";

    private bool _canInteract = false;
    private int[] _arrowSolutions = new int[9];
    private int[] _currentArrowDirections = new int[9];
    private string[] _dirNames = new string[] { "U", "UR", "R", "DR", "D", "DL", "L", "UL" };
    private int? _currentlySelectedArrow;
    private bool[] _isLit = new bool[9];
    private int[] _solutionPositionsForSolveAnim;
    private bool _valid;

    enum ArrowColor
    {
        Black,
        Border,
        White,
        DarkBlue,
        LightBlue,
        Green,
        Orange
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        _loopId = _loopIdCounter++;
        for (int i = 0; i < ArrowSels.Length; i++)
        {
            ArrowSels[i].OnInteract += ArrowPress(i);
            ArrowSels[i].OnHighlight += ArrowHighlight(i);
            ArrowSels[i].OnHighlightEnded += ArrowHighlightEnded(i);
            ArrowObjs[i].transform.GetChild(0).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.Black]);
            ArrowObjs[i].transform.GetChild(0).GetComponent<MeshRenderer>().material.SetTexture(secondary, ColorTextures[(int)ArrowColor.Border]);
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.Black]);
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(secondary, ColorTextures[(int)ArrowColor.DarkBlue]);
            ArrowObjs[i].transform.GetChild(2).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.Black]);
            ArrowObjs[i].transform.GetChild(2).GetComponent<MeshRenderer>().material.SetTexture(secondary, ColorTextures[(int)ArrowColor.White]);
        }
        Module.OnActivate += Activate;

        TryAgain:
        var arrows = new int[9];
        var visited = new List<int>();
        int ix = Rnd.Range(0, 9);
        for (int i = 0; i < 9; i++)
        {
            visited.Add(ix);
            var dir = GetValidDirs(ix).PickRandom();
            arrows[i] = dir;
            var newPos = GetNewPos(ix, dir);
            if (visited.Contains(newPos))
            {
                if (i == 8 && newPos == visited[0])
                    continue;
                goto TryAgain;
            }
            ix = newPos;
        }
        for (int i = 0; i < _arrowSolutions.Length; i++)
            _arrowSolutions[i] = arrows[visited.IndexOf(i)];
        Debug.LogFormat("[Loop #{0}] Solution: {1}", _moduleId, _arrowSolutions.Select(i => _dirNames[i]).Join(", "));
        _currentArrowDirections = _arrowSolutions.ToArray();
        while (IsValidPath(_currentArrowDirections))
            _currentArrowDirections.Shuffle();
    }

    private bool IsValidPath(int[] path)
    {
        var visited = new List<int>();
        int ix = 0;
        for (int i = 0; i < path.Length; i++)
        {
            if (ix < 0 || ix > 8)
                return false;
            visited.Add(ix);
            var newPos = GetNewPos(ix, path[ix]);
            if (visited.Contains(newPos))
            {
                if (i == 8 && newPos == visited[0])
                {
                    _solutionPositionsForSolveAnim = visited.ToArray();
                    return true;
                }
                return false;
            }
            ix = newPos;
        }
        throw new InvalidOperationException("it shouldnt reach here");
    }

    private KMSelectable.OnInteractHandler ArrowPress(int i)
    {
        return delegate ()
        {
            ArrowSels[i].AddInteractionPunch(0.25f);
            if (_moduleSolved || !_canInteract || _valid)
                return false;
            if (_currentlySelectedArrow == null)
            {
                Audio.PlaySoundAtTransform("Click", transform);
                _isLit[i] = true;
                _currentlySelectedArrow = i;
                ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.LightBlue]);
            }
            else if (_currentlySelectedArrow == i)
            {
                Audio.PlaySoundAtTransform("Click", transform);
                _isLit[i] = false;
                _currentlySelectedArrow = null;
                ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.DarkBlue]);
            }
            else
            {
                Audio.PlaySoundAtTransform("Swap", transform);
                var a = i;
                _isLit[i] = true;
                _canInteract = false;
                ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.LightBlue]);
                int dirA = _currentArrowDirections[_currentlySelectedArrow.Value];
                int dirB = _currentArrowDirections[i];
                StartCoroutine(RotateArrow(_currentlySelectedArrow.Value, dirA, dirB));
                StartCoroutine(RotateArrow(i, dirB, dirA));
                _currentArrowDirections[_currentlySelectedArrow.Value] = dirB;
                _currentArrowDirections[i] = dirA;
                _currentlySelectedArrow = null;
                if (IsValidPath(_currentArrowDirections))
                {
                    _valid = true;
                    Audio.PlaySoundAtTransform("Solve", transform);
                    StartCoroutine(SolveAnimation(a));
                }
            }
            return false;
        };
    }

    private Action ArrowHighlight(int i)
    {
        return delegate ()
        {
            if (!_canInteract)
                return;
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.Orange]);
        };
    }

    private Action ArrowHighlightEnded(int i)
    {
        return delegate ()
        {
            if (!_canInteract)
                return;
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[_isLit[i] ? (int)ArrowColor.LightBlue : (int)ArrowColor.DarkBlue]);
        };
    }

    private void Activate()
    {
        if (_loopId == 1)
            Audio.PlaySoundAtTransform("Startup", transform);
        StartCoroutine(StartAnimation());
    }

    private void OnDestroy()
    {
        _loopIdCounter = 1;
    }

    private IEnumerator StartAnimation()
    {
        for (int i = 0; i < 9; i++)
        {
            StartCoroutine(FadeArrowIn(i));
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator RotateArrow(int i, int dirStart, int dirEnd)
    {
        var elapsed = 0f;
        var duration = 0.5f;
        float rotStart = 45f * dirStart;
        float rotEnd = 45f * dirEnd;
        if (dirStart - dirEnd > 4)
            rotEnd += 360f;
        if (dirEnd - dirStart > 4)
            rotStart += 360f;
        while (elapsed < duration)
        {
            ArrowObjs[i].transform.localEulerAngles = new Vector3(0, Easing.InOutQuad(elapsed, rotStart, rotEnd, duration), 0);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ArrowObjs[i].transform.localEulerAngles = new Vector3(0, (rotEnd + 360f) % 360f, 0);
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.DarkBlue]);
        _isLit[i] = false;
        _canInteract = true;
    }

    private IEnumerator FadeArrowIn(int i)
    {
        var elapsed = 0f;
        var duration = 1f;
        while (elapsed < duration)
        {
            ArrowObjs[i].transform.GetChild(0).GetComponent<MeshRenderer>().material.SetFloat("_Blend", elapsed / duration);
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetFloat("_Blend", elapsed / duration);
            ArrowObjs[i].transform.GetChild(2).GetComponent<MeshRenderer>().material.SetFloat("_Blend", elapsed / duration);
            ArrowObjs[i].transform.localEulerAngles = new Vector3(0, Easing.OutQuad(elapsed, 0, 360f + _currentArrowDirections[i] * 45f, duration), 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ArrowObjs[i].transform.GetChild(0).GetComponent<MeshRenderer>().material.SetFloat("_Blend", 1);
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetFloat("_Blend", 0);
        ArrowObjs[i].transform.GetChild(2).GetComponent<MeshRenderer>().material.SetFloat("_Blend", 1);
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(primary, ColorTextures[(int)ArrowColor.DarkBlue]);
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetTexture(secondary, ColorTextures[(int)ArrowColor.Green]);
        if (i == 8)
            _canInteract = true;
    }

    private List<int> GetValidDirs(int pos)
    {
        int r = pos / 3;
        int c = pos % 3;
        var list = new List<int>();
        if (r != 0)
            list.Add(0);
        if (r != 0 && c != 2)
            list.Add(1);
        if (c != 2)
            list.Add(2);
        if (r != 2 && c != 2)
            list.Add(3);
        if (r != 2)
            list.Add(4);
        if (r != 2 && c != 0)
            list.Add(5);
        if (c != 0)
            list.Add(6);
        if (r != 0 && c != 0)
            list.Add(7);
        return list;
    }

    private int GetNewPos(int pos, int dir)
    {
        if (dir == 0)
            return pos - 3;
        if (dir == 1)
            return pos - 2;
        if (dir == 2)
            return pos + 1;
        if (dir == 3)
            return pos + 4;
        if (dir == 4)
            return pos + 3;
        if (dir == 5)
            return pos + 2;
        if (dir == 6)
            return pos - 1;
        if (dir == 7)
            return pos - 4;
        throw new InvalidOperationException("idunno");
    }

    private IEnumerator SolveAnimation(int st)
    {
        int ix = Array.IndexOf(_solutionPositionsForSolveAnim, st);
        for (int i = ix; i < (ix + 9); i++)
        {
            StartCoroutine(SolveArrowTransition(_solutionPositionsForSolveAnim[i % 9]));
            yield return new WaitForSeconds(0.3f);
        }
        yield return new WaitForSeconds(0.25f);
        _moduleSolved = true;
        Module.HandlePass();
    }

    private IEnumerator SolveArrowTransition(int i)
    {
        var duration = 0.7f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetFloat("_Blend", elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ArrowObjs[i].transform.GetChild(1).GetComponent<MeshRenderer>().material.SetFloat("_Blend", 1);
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} swap a1 b2 [Swap positions A1 and B2.] | Acceptable commands include A1-C3, TL-BR, 1-9.";
#pragma warning disable 0414
    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        if (!command.StartsWith("swap "))
            yield break;
        command = command.Substring(4);
        var inputs = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
        if (inputs.Length % 2 != 0)
        {
            yield return "sendtochaterror An odd number of inputs has been entered. Invalid command.";
            yield break;
        }
        var list = new List<int>();
        for (int i = 0; i < inputs.Length; i++)
        {
            if (inputs[i] == "1" || inputs[i] == "tl" || inputs[i] == "a1")
                list.Add(0);
            else if (inputs[i] == "2" || inputs[i] == "tm" || inputs[i] == "b1")
                list.Add(1);
            else if (inputs[i] == "3" || inputs[i] == "tr" || inputs[i] == "c1")
                list.Add(2);
            else if (inputs[i] == "4" || inputs[i] == "ml" || inputs[i] == "a2")
                list.Add(3);
            else if (inputs[i] == "5" || inputs[i] == "mm" || inputs[i] == "b2")
                list.Add(4);
            else if (inputs[i] == "6" || inputs[i] == "mr" || inputs[i] == "c2")
                list.Add(5);
            else if (inputs[i] == "7" || inputs[i] == "bl" || inputs[i] == "a3")
                list.Add(6);
            else if (inputs[i] == "8" || inputs[i] == "bm" || inputs[i] == "b3")
                list.Add(7);
            else if (inputs[i] == "9" || inputs[i] == "br" || inputs[i] == "c3")
                list.Add(8);
            else
            {
                yield return "sendtochaterror \"" + inputs[i] + "\" is an invalid input.";
                yield break;
            }
        }
        yield return null;
        yield return "solve";
        for (int i = 0; i < list.Count; i++)
        {
            while (!_canInteract)
                yield return null;
            ArrowSels[list[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!_canInteract)
            yield return null;
        for (int i = 0; i < 9; i++)
        {
            if (_currentArrowDirections[i] != _arrowSolutions[i])
            {
                for (int j = i; j < 9; j++)
                    if (_currentArrowDirections[j] == _arrowSolutions[i])
                    {
                        ArrowSels[i].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                        ArrowSels[j].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                        while (!_canInteract)
                            yield return null;
                        goto nextIter;
                    }
            }
            nextIter:;
        }
        while (!_moduleSolved)
            yield return true;
    }
}