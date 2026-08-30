#!/usr/bin/env bash

# Shared build orchestration. Entry scripts keep their product-specific arguments and
# packaging, while staging, disk checks, Unity discovery and font preparation live here.

ssnoir_build_init() {
    local profile="$1"
    SSNOIR_REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
    SSNOIR_SOURCE_PROJECT="$SSNOIR_REPO_ROOT/UnityClient"
    SSNOIR_PROFILE_CACHE_ROOT="$SSNOIR_REPO_ROOT/.cache/build/$profile"
    SSNOIR_STAGING_PROJECT="$SSNOIR_PROFILE_CACHE_ROOT/UnityClient"
    SSNOIR_GENERATED_FONT_DIR="$SSNOIR_PROFILE_CACHE_ROOT/generated-fonts"
    SSNOIR_RESOURCE_PLAN="$SSNOIR_REPO_ROOT/tools/build/resource-plan.json"
}

ssnoir_require_build_space() {
    local label="$1"
    local available_kb
    local required_kb=$((2 * 1024 * 1024))
    available_kb="$(df -Pk "$SSNOIR_REPO_ROOT" | awk 'NR == 2 { print $4 }')"
    if [[ "$available_kb" -lt "$required_kb" ]]; then
        local available_mb=$((available_kb / 1024))
        echo "磁盘可用空间只有 ${available_mb} MiB；${label} 至少预留 2048 MiB。" >&2
        return 3
    fi
}

ssnoir_prepare_staging() {
    local label="$1"
    mkdir -p "$SSNOIR_PROFILE_CACHE_ROOT"

    if [[ ! -d "$SSNOIR_STAGING_PROJECT" ]]; then
        echo "[$label] 首次创建独立 APFS staging 快照..."
        cp -cR "$SSNOIR_SOURCE_PROJECT" "$SSNOIR_STAGING_PROJECT"
    fi

    echo "[$label] 同步 Unity 主工程到独立 staging..."
    rsync -a --delete \
        --exclude '/Library/' \
        --exclude '/Temp/' \
        --exclude '/Logs/' \
        --exclude '/Build/' \
        --exclude '/Builds/' \
        --exclude '/UserSettings/' \
        --exclude '/Screenshots/' \
        --exclude '/CutsceneSource~/' \
        "$SSNOIR_SOURCE_PROJECT/" "$SSNOIR_STAGING_PROJECT/"

    rm -rf \
        "$SSNOIR_STAGING_PROJECT/Build" \
        "$SSNOIR_STAGING_PROJECT/Builds" \
        "$SSNOIR_STAGING_PROJECT/Logs" \
        "$SSNOIR_STAGING_PROJECT/Temp" \
        "$SSNOIR_STAGING_PROJECT/Screenshots" \
        "$SSNOIR_STAGING_PROJECT/CutsceneSource~"
}

ssnoir_require_unity() {
    SSNOIR_UNITY_EXECUTABLE="${UNITY_PATH:-/Applications/Unity/Unity.app/Contents/MacOS/Unity}"
    if [[ ! -x "$SSNOIR_UNITY_EXECUTABLE" ]]; then
        echo "找不到 Unity。请通过 UNITY_PATH 指定 Unity 可执行文件。" >&2
        return 2
    fi
}

ssnoir_run_unity() {
    local execute_method="$1"
    local log_path="$2"
    shift 2

    env "$@" \
        "$SSNOIR_UNITY_EXECUTABLE" \
        -batchmode \
        -quit \
        -nographics \
        -buildTarget WebGL \
        -projectPath "$SSNOIR_STAGING_PROJECT" \
        -executeMethod "$execute_method" \
        -logFile "$log_path"
}

ssnoir_remove_build_path() {
    local target="$1"
    [[ -e "$target" ]] || return 0

    # 调用方只会传入已校验名称的构建产物；直接删除才能真正释放磁盘空间。
    rm -rf -- "$target"
}

ssnoir_cleanup_web_release_history() {
    local output_root="$1"
    local current_stamp="$2"
    local candidate name
    [[ -d "$output_root" ]] || return 0

    while IFS= read -r -d '' candidate; do
        name="${candidate##*/}"
        case "$name" in
            "$current_stamp"|"$current_stamp-offline"|"$current_stamp.build.log"|\
            "SSNoir-WebDemo-$current_stamp.zip"|"SSNoir-ItchWeb-$current_stamp.zip")
                continue
                ;;
        esac

        if [[ "$name" =~ ^[0-9]{8}-[0-9]{6}(-offline|\.build\.log)?$ \
            || "$name" =~ ^(SSNoir-WebDemo|SSNoir-ItchWeb)-[0-9]{8}-[0-9]{6}\.zip$ ]]; then
            echo "[Build] 清理旧 Web Release: $candidate"
            ssnoir_remove_build_path "$candidate"
        fi
    done < <(find "$output_root" -mindepth 1 -maxdepth 1 -print0)
}

ssnoir_cleanup_timestamped_releases() {
    local output_root="$1"
    local current_output="$2"
    local candidate name
    [[ -d "$output_root" ]] || return 0

    while IFS= read -r -d '' candidate; do
        [[ "$candidate" == "$current_output" ]] && continue
        name="${candidate##*/}"
        [[ "$name" =~ ^[0-9]{8}-[0-9]{6}$ ]] || continue
        echo "[Build] 清理旧 Release: $candidate"
        ssnoir_remove_build_path "$candidate"
    done < <(find "$output_root" -mindepth 1 -maxdepth 1 -type d -print0)
}

ssnoir_generate_font_subset() {
    local label="$1"
    [[ -f "$SSNOIR_RESOURCE_PLAN" ]] || {
        echo "找不到公共资源计划: $SSNOIR_RESOURCE_PLAN" >&2
        return 2
    }
    echo "[$label] 扫描文字并生成 SourceHan 字体子集..."
    python3 "$SSNOIR_REPO_ROOT/tools/build/subset_fonts.py" \
        --repo-root "$SSNOIR_REPO_ROOT" \
        --output-dir "$SSNOIR_GENERATED_FONT_DIR"
}
