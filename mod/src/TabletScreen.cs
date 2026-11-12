// Probably Doom
// Copyright (C) 2026 dbfoxtw
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

namespace ProbablyDoom
{
    using UnityEngine;

    /// <summary>
    /// 平板本體（店裡右邊桌上那台，場景的 NegociationTablet，掛 StoreTablet）螢幕上的小畫面（需求.md 決定 6～8、28）。
    /// 平板是一張 960×540 的整張圖層（SpriteRenderer，排序 10、沒有子物件），螢幕在圖上 79×55 遊戲像素的地方（TabletGeometry）。
    /// 加一個子物件掛 SpriteRenderer：同一個排序圖層、順序比本體大 1、同一個材質（燈光跟著場景），sprite 是 79×55 的貼圖
    /// （DoomSession.Thumb，Point），每像素單位和本體一樣，所以一格對一格蓋在原本的「Useful info」上。
    /// 貼圖是 RGBA32，下緣兩角的支架與橘色陰影線那些格是透明的（alpha 0），露出原圖（決定 28）：本體的材質本來就畫透明的圖層
    /// （整張 960×540 大部分是透明的），所以用同一個材質就有 alpha 混合。
    /// 子物件沒有 Collider：遊戲判斷點到店裡的東西靠 EventSystem 對 Collider2D 的射線（Interactable 的 pointer 事件；
    /// 平板本體有兩個 BoxCollider2D），所以小畫面不影響點平板、平板的提示框。
    /// 只在「正在玩」（Doom 模式＋有焦點）時顯示，其他時候隱藏，螢幕回到原本的畫面（決定 8）。設定關掉時完全不建（決定 7）。
    /// </summary>
    sealed class TabletScreen
    {
        readonly GameObject _go;

        TabletScreen(GameObject go)
        {
            _go = go;
        }

        /// <summary>建在平板本體底下（先隱藏）。找不到平板的 SpriteRenderer 或圖時回傳 null、記 log。</summary>
        internal static TabletScreen Create(StoreTablet tablet, Texture2D thumb)
        {
            var body = tablet.GetComponent<SpriteRenderer>();
            if (body == null || body.sprite == null)
            {
                ProbablyDoomMod.Warning("平板本體沒有 SpriteRenderer 或圖，不加小畫面");
                return null;
            }
            var s = body.sprite;
            var ppu = s.pixelsPerUnit;
            TabletGeometry.ScreenCenter(s.rect.width, s.rect.height, s.pivot.x, s.pivot.y, ppu, out var x, out var y);
            if (body.flipX) x = -x;
            if (body.flipY) y = -y;

            var go = new GameObject("ProbablyDoom.TabletScreen");
            go.layer = tablet.gameObject.layer; // 相機照圖層決定畫不畫，和本體一樣
            go.transform.SetParent(tablet.transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(thumb, new Rect(0, 0, thumb.width, thumb.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder + 1;
            sr.sharedMaterial = body.sharedMaterial;
            sr.flipX = body.flipX;
            sr.flipY = body.flipY;
            go.SetActive(false);
            ProbablyDoomMod.DebugLog($"平板本體的小畫面：本地座標 ({x:0.###}, {y:0.###})，平板的圖 {s.rect.width}x{s.rect.height}、pivot ({s.pivot.x}, {s.pivot.y})、ppu {ppu}");
            return new TabletScreen(go);
        }

        /// <summary>物件還在（跟著店面場景銷毀）。</summary>
        internal bool Alive => _go != null;

        internal void SetVisible(bool visible)
        {
            if (_go != null && _go.activeSelf != visible) _go.SetActive(visible);
        }

        internal void Destroy()
        {
            if (_go != null) Object.Destroy(_go);
        }
    }
}
