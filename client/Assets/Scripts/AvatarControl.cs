using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class AvatarControl : MonoBehaviour
{

    public TextAsset bsname;
    private List<string> ue4BsList;
    public AudioSource audio;
    //头部mesh
    public SkinnedMeshRenderer headSkinMesh;


    // TODO需要修改对应映射
    private Dictionary<string, string> UE2ARkit = new Dictionary<string, string> {
        {"jawForward"   ,"jaw_thrust_c"},
        {"jawLeft"      ,"jaw_sideways_l"},
        {"jawRight"     ,"jaw_sideways_r"    },
        {"jawOpen"      ,"mouth_stretch_c"   },
        {"mouthClose"   ,"mouth_chew_c"  },
        {"mouthFunnel"  ,"mouth_funnel_dl,mouth_funnel_dr,mouth_funnel_ul,mouth_funnel_ur"   },
        {"mouthPucker"  ,"mouth_pucker_l,mouth_pucker_r"     },
        {"mouthLeft"    ,"mouth_sideways_l"          },
        {"mouthRight"   ,"mouth_sideways_r"      },
        {"mouthSmileLeft"       ,"mouth_lipCornerPull_l"     },
        {"mouthSmileRight"      ,"mouth_lipCornerPull_r" },
        {"mouthFrownLeft"       ,"mouth_lipCornerDepress_l,mouth_lipCornerDepressFix_l"},
        {"mouthFrownRight"      ,"mouth_lipCornerDepress_r,mouth_lipCornerDepressFix_r"},
        {"mouthDimpleLeft"      ,"mouth_dimple_l"},
        {"mouthDimpleRight"     ,"mouth_dimple_r"},
        {"mouthStretchLeft"     ,"mouth_lipStretch_l"},
        {"mouthStretchRight"    ,"mouth_lipStretch_r"},
        {"mouthRollLower"       ,"mouth_suck_dl,mouth_suck_dr"},
        {"mouthRollUpper"       ,"mouth_suck_ul,mouth_suck_ur"},
        {"mouthShrugLower"      ,"mouth_chinRaise_d"     },
        {"mouthShrugUpper"      ,"mouth_chinRaise_u"     },
        {"mouthPressLeft"       ,"mouth_press_l"     },
        {"mouthPressRight"      ,"mouth_press_r" },
        {"mouthLowerDownLeft"   ,"mouth_lowerLipDepress_l"   },
        {"mouthLowerDownRight"  ,"mouth_lowerLipDepress_r"},
        {"mouthUpperUpLeft"     ,"mouth_upperLipRaise_l" },
        {"mouthUpperUpRight"    ,"mouth_upperLipRaise_r" },
    };



    private Dictionary<string, string> modelBsName2ARKit = new Dictionary<string, string> { };

    void Start()
    {
        //设置游戏帧率30fps
        Application.targetFrameRate = 30;

        //读取配置文件
        ue4BsList = bsname.text.Split("\n").ToList();
    }

    // Update is called once per frame

    float time = 0;
    void Update()
    {
        if (headSkinMesh != null) {
            time += Time.deltaTime;
            //每隔3秒播放一次眨眼动画
            if (time >= 3) {
                time = 0.0f;
                StartCoroutine(BlinkEye());
            }
        }
    }

    //眨眼动画
    private IEnumerator BlinkEye() {
        List<float> weight = new List<float> { 0, 20, 40, 60, 80, 100, 80, 60, 40, 20, 0 };
        for (var k = 0; k < weight.Count; k++) {
            headSkinMesh.SetBlendShapeWeight(23, weight[k]);
            yield return new WaitForEndOfFrame();
        }
    }


    public IEnumerator SetBsWeight(List<List<float>> valueList, List<string> ue4BsList) {
        SkinnedMeshRenderer smr = headSkinMesh;
        //CharacterAnimation.RunAnimation(1000);
        int bsCount = smr.sharedMesh.blendShapeCount;
        for (int i = 0; i < valueList.Count; i++) {

            List<float> list = valueList[i];
            for (int j = 0; j < bsCount; j++) {
                string bsName = smr.sharedMesh.GetBlendShapeName(j);
                modelBsName2ARKit.TryGetValue(bsName, out string artKitName);
                if (!string.IsNullOrEmpty(artKitName)) {
                    UE2ARkit.TryGetValue(artKitName, out string ue4Bs116Name);

                    if (!string.IsNullOrEmpty(ue4Bs116Name)) {

                        string[] ue4Bs116Names = ue4Bs116Name.Split(",");
                        float weight = 0;
                        if (ue4Bs116Names.Length == 1) {
                            int ue4bsIndex = ue4BsList.FindIndex(e => e == ue4Bs116Name);
                            if (ue4bsIndex >= 0 && ue4bsIndex < list.Count) {
                                weight = Remap(list[ue4bsIndex]);
                            }

                        } else {
                            weight = 1;
                            List<float> weightList = new List<float> { };
                            for (var k = 0; k < 4; k++) {
                                int ue4bsIndex = ue4BsList.FindIndex(e => e == ue4Bs116Name);
                                if (ue4bsIndex == -1) {
                                    weightList.Add(0);
                                } else {
                                    weight = Remap(list[ue4bsIndex]);
                                    weightList.Add(weight);
                                }
                            }
                            weight = Mathf.Max(Remap(weightList[0]), Remap(weightList[1]), Remap(weightList[2]), Remap(weightList[3]));
                        }

                        smr.SetBlendShapeWeight(j, weight);
                    }
                }
            }
            if (i == valueList.Count - 1) {
                //CharacterAnimation.RunAnimation(0);
            }
            yield return new WaitForEndOfFrame();
        }
    }
    private float Remap(float v) {
        return v * 150;
    }


    //请求
    public void SendMsg(string msg) {
        //设置服务器地址
        string bsUrl = "http://10.61.160.206:8000?text=" + msg;
        StartCoroutine(RequestAudioAndWeight(bsUrl, msg));
    }

    IEnumerator RequestAudioAndWeight(string url, string msg) {
        using (UnityWebRequest www = UnityWebRequest.Get(url)) {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success) {
                Debug.LogError(www.error);
            } else {
                //获取响应头中的内容类型
                string contentType = www.GetResponseHeader("Content-Type");
                if (contentType == "application/json") {
                    //将响应内容转换成json格式的字符串
                    string jsonStr = www.downloadHandler.text;
                    Debug.Log(jsonStr);

                    //解析json字符串，获取二进制数据和权重数据
                    JObject jsonObject = JObject.Parse(jsonStr);
                    byte[] audioData = Encoding.GetEncoding("iso-8859-1").GetBytes(jsonObject.GetValue("audio").ToString());
                    Debug.Log("dwx   len" + audioData.Length);
                    string weightData = jsonObject.GetValue("weight").ToString();
                    AudioClip audioClip = ConvertBytesToClip(audioData);
                    if (audioClip != null) {
                        audio.clip = audioClip;
                    }
                    UpdateModelBsWeight(weightData);
                    Debug.Log("dwx 播放音频");
                    audio.Play();


                } else {
                    Debug.LogError("Invalid Content-Type: " + contentType);
                }
            }
        }
    }

    private void UpdateModelBsWeight(string weights) {

        List<List<float>> valueList = new List<List<float>>();
        List<string> weightPre = weights.Split("\n").ToList();
        for (int i = 0; i < weightPre.Count; i++) {
            List<string> weight = weightPre[i].Split(',').ToList();
            List<float> list = new List<float>();
            for (int j = 0; j < weight.Count; j++) {
                float.TryParse(weight[j], out float result);
                list.Add(result);
            }
            valueList.Add(list);
        }

        StartCoroutine(SetBsWeight(valueList, ue4BsList));

    }


    public AudioClip ConvertBytesToClip(byte[] rawData) {
        float[] samples = new float[rawData.Length / 2];
        float rescaleFactor = 32767;
        short st = 0;
        float ft = 0;

        for (int i = 0; i < rawData.Length; i += 2) {
            st = BitConverter.ToInt16(rawData, i);
            ft = st / rescaleFactor;
            samples[i / 2] = ft;
        }

        AudioClip audioClip = AudioClip.Create("mySound", samples.Length, 1, 16000, false);
        audioClip.SetData(samples, 0);
        Debug.Log(audioClip.length);
        return audioClip;
    }



}
