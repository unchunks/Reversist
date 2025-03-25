using DG.Tweening;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace App.Reversi
{
    public class Nail : MonoBehaviour
    {
        [SerializeField] private GameObject _childNail;

        public async void Pin()
        {
            _childNail.transform.localPosition = new Vector3(0, 3, 0);
            await _childNail.transform.DOLocalMoveY(0, 0.1f)
                            .SetEase(Ease.Linear)
                            .ToUniTask();
            _childNail.transform.localPosition = new Vector3(0, 0, 0);
        }
    }
}