using UnityEngine;
using TMPro;
using System.Collections;

public class ConsensusTitleEffect : MonoBehaviour
{
    private TMP_Text textComponent;
    private string originalText;
    private string chars = "!@#$%^&*()_+-=[]{}|;':,.<>/?0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    [Header("Decoding Settings")]
    public float decodeSpeed = 0.05f;

    [Header("Glitch Settings")]
    public float glitchChance = 0.1f;
    public float glitchIntensity = 2f;

    void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
        originalText = textComponent.text;
    }

    void Start()
    {
        StartCoroutine(ExecuteDecode());
    }

    // 1. 字符解码效果
    IEnumerator ExecuteDecode()
    {
        int length = originalText.Length;
        char[] readyText = new char[length];

        for (int i = 0; i < length; i++)
        {
            float timer = 0;
            while (timer < 0.5f) // 每个字母乱码滚动0.5秒
            {
                readyText[i] = chars[Random.Range(0, chars.Length)];
                textComponent.text = new string(readyText);
                timer += decodeSpeed;
                yield return new WaitForSeconds(decodeSpeed);
            }
            readyText[i] = originalText[i]; // 定格正确字符
        }
        textComponent.text = originalText;

        // 解码完成后，开启持续的抖动
        StartCoroutine(ContinuousGlitch());
    }

    // 2. 持续的顶点错位效果 (Unity TMP 特有)
    IEnumerator ContinuousGlitch()
    {
        while (true)
        {
            if (Random.value < glitchChance)
            {
                textComponent.ForceMeshUpdate();
                var textInfo = textComponent.textInfo;

                // 随机选择一个字符进行偏移
                int charIndex = Random.Range(0, textInfo.characterCount);
                if (!textInfo.characterInfo[charIndex].isVisible) continue;

                int materialIndex = textInfo.characterInfo[charIndex].materialReferenceIndex;
                int vertexIndex = textInfo.characterInfo[charIndex].vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                Vector3 offset = new Vector3(Random.Range(-glitchIntensity, glitchIntensity), 0, 0);

                for (int j = 0; j < 4; j++)
                {
                    vertices[vertexIndex + j] += offset;
                }

                textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                yield return new WaitForSeconds(0.1f); // 抖动持续时间
            }
            yield return new WaitForSeconds(Random.Range(0.5f, 2.0f)); // 抖动间隔
        }
    }
}