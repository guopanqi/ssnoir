#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// City.fbx is the world layer: base, fill, every place's shell, far outlines and all semantic
    /// nodes (Anchor / Camera / OrbitPivot / PanBounds). Each top-level place's detail — its
    /// focus outline and interior — ships as its own asset, Resources/City/Places/&lt;名&gt;.fbx,
    /// whose root is the same node as the shell root (same name, same transform). This class
    /// instantiates those assets as siblings of their shells under City. Splitting the files is
    /// what lets later places move out of the first package; today everything is local and is
    /// attached once at startup.
    /// </summary>
    public static class CityPlaces
    {
        public const string ResourcesFolder = "City/Places/";

        /// <summary>
        /// The city base (CityBox role="base"): carries the overview camera and anchor but is the
        /// world layer itself — no shell / detail split, no outline, no detail asset.
        /// </summary>
        public const string WorldBaseName = "世界";

        /// <summary>Top-level City children that are places: they own at least one focus camera and are not the base.</summary>
        public static IEnumerable<Transform> ShellRoots(Transform cityRoot)
        {
            foreach (Transform child in cityRoot)
            {
                if (child.name == WorldBaseName)
                    continue;
                if (child.GetComponentInChildren<CinemachineVirtualCamera>(true) != null)
                    yield return child;
            }
        }

        /// <summary>
        /// 运行时只信自己挂的：先把 City 下所有细节实例（编辑器预览的、上次异常留下的，一律带
        /// <see cref="CityPlaceDetail"/>）清掉，再给每个壳挂一份新的。不数同名、不看 hideFlags——
        /// 那两种判断都曾漏过孤儿。缺资产是契约错误。
        /// </summary>
        public static void AttachAll(Transform cityRoot)
        {
            DetachAll(cityRoot);
            foreach (var shell in ShellRoots(cityRoot).ToArray())
            {
                // 同名却既不是壳、也没有标记的对象：多半是手动复制或早年漏出来后被存进场景的孤儿。
                // 这种东西运行时不能替人删（它在场景文件里），只能点名让人去场景里删。
                foreach (Transform child in cityRoot)
                    if (child != shell && child.name == shell.name)
                        throw new InvalidOperationException(
                            $"[SSNoir] City 下有多余的 '{shell.name}' 节点（不是壳、也不是运行时挂的细节）。在场景 Hierarchy 里删掉它。");
                var asset = Resources.Load<GameObject>(ResourcesFolder + shell.name);
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        $"[SSNoir] City place '{shell.name}' has no detail asset at Resources/{ResourcesFolder}{shell.name}.fbx. Rebuild and publish CityBox.");
                }
                Attach(cityRoot, shell.name, asset);
            }
        }

        /// <summary>City 下现存的细节实例（按标记组件认，不按名字）。</summary>
        public static IEnumerable<Transform> DetailInstances(Transform cityRoot)
        {
            foreach (Transform child in cityRoot)
                if (child != null && child.GetComponent<CityPlaceDetail>() != null)
                    yield return child;
        }

        public static void DetachAll(Transform cityRoot)
        {
            foreach (var detail in DetailInstances(cityRoot).ToArray())
                UnityEngine.Object.DestroyImmediate(detail.gameObject);
        }

        /// <summary>标记和 hideFlags 先打、动画后装：装动画抛了就把实例销毁再抛，City 下不留半成品。</summary>
        public static GameObject Attach(Transform cityRoot, string placeName, GameObject asset, HideFlags hideFlags = HideFlags.None)
        {
            var instance = UnityEngine.Object.Instantiate(asset, cityRoot);
            instance.name = placeName;
            instance.hideFlags = hideFlags;
            instance.AddComponent<CityPlaceDetail>();
            try
            {
                PropMotion.Attach(instance, placeName);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(instance);
                throw;
            }
            return instance;
        }
    }

    /// <summary>标记：这个对象是 <see cref="CityPlaces.Attach"/> 挂上来的地点细节实例，不是 City.fbx 里的壳。</summary>
    public sealed class CityPlaceDetail : MonoBehaviour
    {
    }
}
