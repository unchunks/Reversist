using System.Collections;
using TMPro;
using UnityEngine;

public class Stone : MonoBehaviour
{
    public StoneType Type { get; set; }

    private GameObject nail;
    private TextMeshPro countText;
    private int Count { get; set; }

    private State _up = State.None;

    private Animator _animator;

    public void Init(State up, StoneType type)
    {
        _up = up;
        Type = type;

        _animator = GetComponent<Animator>();

        nail = transform.Find("Nail").gameObject;
        if (nail != null)
        {
            nail.SetActive(false);
        }
        countText = transform.Find("CountText").GetComponent<TextMeshPro>();
        if (countText != null)
        {
            countText.gameObject.SetActive(false);
        }

        switch (Type)
        {
            case StoneType.Normal: break;
            case StoneType.Reverse: break;
            case StoneType.DelayReverse:
                StartCoroutine(WaitTimeCount());
                break;
            case StoneType.Frozen:
                StartCoroutine(WaitPinStone());
                break;
        }
    }

    public void Twitch()
    {
        _animator.Play("TwitchStone");
    }

    public void Flip()
    {
        if (Type == StoneType.Frozen)
        {
            return;
        }

        if (Count > 0)
        {
            Count--;
            countText.text = Count.ToString();
            if (Count == 0)
            {
                countText.gameObject.SetActive(false);
            }
        }

        if (_up == State.Black)
        {
            _animator.Play("BlackToWhite");
            _up = State.White;
        }
        else if (_up == State.White)
        {
            _animator.Play("WhiteToBlack");
            _up = State.Black;
        }
    }

    private void PinStone()
    {
        nail.SetActive(true);
        _animator.Play("PinStone");
    }

    private IEnumerator WaitPinStone()
    {
        yield return new WaitForSeconds(0.30f);
        PinStone();
        yield return new WaitForEndOfFrame();
    }

    private void TimeCount()
    {
        Count = 1;
        countText.gameObject.SetActive(true);
        countText.text = Count.ToString();
        _animator.Play("CountEffect");
    }

    private IEnumerator WaitTimeCount()
    {
        yield return new WaitForSeconds(0.30f);
        TimeCount();
        yield return new WaitForEndOfFrame();
    }
}
