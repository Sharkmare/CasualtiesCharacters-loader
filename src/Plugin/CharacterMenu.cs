using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesCharacters
{
    public static class CharacterMenu
    {
        const int FirstCustomIndex = 3;
        static readonly Color SelectedTint = Color.white;
        static readonly Color IdleTint = new Color(0.5f, 0.5f, 0.5f);
        static readonly Color IdleInnerTint = new Color(0.3f, 0.3f, 0.3f);

        public static void Build(PreRunScript menu)
        {
            if (Registry.HighestId < FirstCustomIndex || menu.charImages.Count < FirstCustomIndex) return;
            if (menu.charImages.Count > FirstCustomIndex) return;

            Image sourceButton = menu.charImages[2];
            Vector2 step = sourceButton.rectTransform.anchoredPosition - menu.charImages[1].rectTransform.anchoredPosition;
            for (int index = FirstCustomIndex; index <= Registry.HighestId; index++)
            {
                bool real = Registry.Get(index) != null;
                GameObject buttonClone = Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
                buttonClone.name = "CustomCharacter" + index;
                RectTransform rect = buttonClone.GetComponent<RectTransform>();
                rect.anchoredPosition = sourceButton.rectTransform.anchoredPosition + step * (index - 2);

                menu.charImages.Add(buttonClone.GetComponent<Image>());
                menu.charInnerImages.Add(CloneRelated(menu.charInnerImages[2], sourceButton, buttonClone, step * (index - 2)));
                menu.charPodiums.Add(CloneRelated(menu.charPodiums[2], sourceButton, buttonClone, step * (index - 2)));

                UITooltip tooltip = buttonClone.GetComponent<UITooltip>();
                if (tooltip != null) Object.Destroy(tooltip);
                Button button = buttonClone.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                int captured = index;
                button.onClick.AddListener(() => menu.SelectCharacter(captured));
                button.interactable = real;
                buttonClone.SetActive(real);
            }
            Plugin.Log.LogInfo("character menu: " + (menu.charImages.Count - FirstCustomIndex) + " custom entries added");
        }

        static Image CloneRelated(Image source, Image sourceButton, GameObject buttonClone, Vector2 offset)
        {
            if (source.transform.IsChildOf(sourceButton.transform))
                return FollowPath(buttonClone.transform, source.transform, sourceButton.transform).GetComponent<Image>();
            GameObject clone = Object.Instantiate(source.gameObject, source.transform.parent);
            clone.GetComponent<RectTransform>().anchoredPosition = source.rectTransform.anchoredPosition + offset;
            clone.SetActive(buttonClone.activeSelf);
            return clone.GetComponent<Image>();
        }

        static Transform FollowPath(Transform cloneRoot, Transform target, Transform sourceRoot)
        {
            Stack<int> path = new Stack<int>();
            for (Transform step = target; step != sourceRoot; step = step.parent) path.Push(step.GetSiblingIndex());
            Transform walker = cloneRoot;
            while (path.Count > 0) walker = walker.GetChild(path.Pop());
            return walker;
        }

        public static void Colour(PreRunScript menu)
        {
            for (int index = FirstCustomIndex; index < menu.charImages.Count; index++)
            {
                if (Registry.Get(index) == null) continue;
                bool selected = index == PreRunScript.characterSelected;
                menu.charImages[index].color = selected ? SelectedTint : IdleTint;
                menu.charInnerImages[index].color = selected ? SelectedTint : IdleInnerTint;
                float alpha = selected ? Mathf.PingPong(Time.unscaledTime * 0.5f, 0.5f) : 0f;
                menu.charPodiums[index].color = new Color(1f, 1f, 1f, alpha);
            }
        }
    }
}
