-- Copy this file to: <RE4 game folder>/reframework/autorun/
-- A lightweight cinematic camera rig for trailer shots.

local SETTINGS_PATH = "re4r_cinematic_camera_rig.json"

local MODE_CRANE = 1
local MODE_KEYFRAMES = 2
local MODES = {
    "Crane Follow",
    "Keyframe Rail"
}

local settings = {
    enabled = false,
    mode = MODE_CRANE,
    use_player_as_target = true,
    target_x = 0.0,
    target_y = 0.0,
    target_z = 0.0,
    target_offset_y = 1.45,
    radius = 7.0,
    height = 2.4,
    yaw_degrees = 35.0,
    orbit_speed_degrees = 4.0,
    crane_speed = 0.0,
    dolly_speed = 0.0,
    min_radius = 1.5,
    max_radius = 35.0,
    min_height = -4.0,
    max_height = 16.0,
    position_smoothness = 4.0,
    target_smoothness = 6.0,
    invert_look_direction = false,
    apply_fov = false,
    fov = 45.0,
    fov_smoothness = 3.5,
    playback_speed = 1.0,
    loop_keyframes = false,
    captured_keyframe_duration = 3.0,
    draw_status = true,
    keyframes = {}
}

local state = {
    frame = 0,
    status = "Disabled",
    smoothed_position = nil,
    smoothed_target = nil,
    smoothed_fov = nil,
    desired_position = nil,
    desired_target = nil,
    playing = false,
    play_index = 1,
    play_time = 0.0,
    notification = "",
    notification_frames = 0
}

local character_manager = nil
local game_object_type = nil
local get_transform_method = nil
local scene_manager = nil
local scene_manager_type = nil

local unpack_args = table.unpack or unpack

local function safe_call(fn)
    local ok, value = pcall(fn)
    if ok then
        return value
    end
    return nil
end

local function call_method(obj, method_name, ...)
    if obj == nil then
        return nil
    end

    local args = {...}
    return safe_call(function()
        return obj:call(method_name, unpack_args(args))
    end)
end

local function try_call_method(obj, method_name, ...)
    if obj == nil then
        return false, nil
    end

    local args = {...}
    local ok, value = pcall(function()
        return obj:call(method_name, unpack_args(args))
    end)
    return ok, value
end

local function copy_defaults(target, defaults)
    for key, value in pairs(defaults) do
        if target[key] == nil then
            target[key] = value
        end
    end
end

local function notify(message)
    state.status = message
    state.notification = message
    state.notification_frames = 150
end

local function load_settings()
    local loaded = safe_call(function()
        return json.load_file(SETTINGS_PATH)
    end)

    if type(loaded) == "table" then
        copy_defaults(loaded, settings)
        if type(loaded.keyframes) ~= "table" then
            loaded.keyframes = {}
        end
        settings = loaded
        notify("Loaded settings")
    end
end

local function save_settings()
    safe_call(function()
        json.dump_file(SETTINGS_PATH, settings)
    end)
end

local function clamp(value, min_value, max_value)
    if value < min_value then
        return min_value
    end
    if value > max_value then
        return max_value
    end
    return value
end

local function deg_to_rad(value)
    return value * 0.017453292519943295
end

local function exp_smooth_alpha(smoothness)
    smoothness = tonumber(smoothness) or 0.0
    if smoothness <= 0.0 then
        return 1.0
    end
    return 1.0 - math.exp(-smoothness / 60.0)
end

local function smoothstep(t)
    t = clamp(t, 0.0, 1.0)
    return t * t * (3.0 - (2.0 * t))
end

local function vec3(x, y, z)
    return {
        x = tonumber(x) or 0.0,
        y = tonumber(y) or 0.0,
        z = tonumber(z) or 0.0
    }
end

local function copy_vec3(value)
    if value == nil then
        return nil
    end
    return vec3(value.x, value.y, value.z)
end

local function read_vector3(value)
    if value == nil then
        return nil
    end

    local x = value.x or value.X
    local y = value.y or value.Y
    local z = value.z or value.Z

    if x == nil then
        x = safe_call(function() return value:get_field("x") end) or
            safe_call(function() return value:get_field("X") end)
    end
    if y == nil then
        y = safe_call(function() return value:get_field("y") end) or
            safe_call(function() return value:get_field("Y") end)
    end
    if z == nil then
        z = safe_call(function() return value:get_field("z") end) or
            safe_call(function() return value:get_field("Z") end)
    end

    if x ~= nil and y ~= nil and z ~= nil then
        return vec3(x, y, z)
    end

    return nil
end

local function lerp_number(a, b, t)
    return a + ((b - a) * t)
end

local function lerp_vec3(a, b, t)
    if a == nil then
        return copy_vec3(b)
    end
    if b == nil then
        return copy_vec3(a)
    end
    return {
        x = lerp_number(a.x, b.x, t),
        y = lerp_number(a.y, b.y, t),
        z = lerp_number(a.z, b.z, t)
    }
end

local function vector_length(value)
    return math.sqrt((value.x * value.x) + (value.y * value.y) + (value.z * value.z))
end

local function normalize_vec3(value)
    local length = vector_length(value)
    if length < 0.0001 then
        return vec3(0.0, 0.0, -1.0)
    end
    return {
        x = value.x / length,
        y = value.y / length,
        z = value.z / length
    }
end

local function init_types()
    game_object_type = game_object_type or safe_call(function()
        return sdk.find_type_definition("via.GameObject")
    end)
    get_transform_method = get_transform_method or (game_object_type and safe_call(function()
        return game_object_type:get_method("get_Transform")
    end) or nil)
    scene_manager = scene_manager or safe_call(function()
        return sdk.get_native_singleton("via.SceneManager")
    end)
    scene_manager_type = scene_manager_type or safe_call(function()
        return sdk.find_type_definition("via.SceneManager")
    end)
end

local function get_character_manager()
    character_manager = character_manager or safe_call(function()
        return sdk.get_managed_singleton("chainsaw.CharacterManager")
    end)
    return character_manager
end

local function get_position(obj, depth)
    depth = depth or 0
    if obj == nil or depth > 4 then
        return nil
    end

    local direct_methods = {
        "get_Position",
        "get_WorldPosition",
        "get_Pos",
        "get_UniversalPosition"
    }

    for _, method_name in ipairs(direct_methods) do
        local position = read_vector3(call_method(obj, method_name))
        if position ~= nil then
            return position
        end
    end

    local child_methods = {
        "get_Transform",
        "get_GameObject",
        "get_GameObjectRef",
        "get_BodyGameObject",
        "get_ValidBodyGameObject",
        "get_CharaGameObject",
        "get_Body"
    }

    for _, method_name in ipairs(child_methods) do
        local child = call_method(obj, method_name)
        if child ~= nil and child ~= obj then
            local position = get_position(child, depth + 1)
            if position ~= nil then
                return position
            end
        end
    end

    local native_position = safe_call(function()
        return obj:get_position()
    end)
    native_position = read_vector3(native_position)
    if native_position ~= nil then
        return native_position
    end

    return nil
end

local function get_player_position()
    local manager = get_character_manager()
    if manager == nil then
        return nil
    end

    local player = safe_call(function()
        return manager:getPlayerContextRef()
    end) or call_method(manager, "getPlayerContextRef") or
        call_method(manager, "get_MasterPlayer") or
        call_method(manager, "get_MainPlayer")

    local position = get_position(player)
    if position ~= nil then
        return position
    end

    local players = call_method(manager, "get_PlayerAndPartnerContextList") or
        call_method(manager, "get_PlayerContextList")
    local count = tonumber(call_method(players, "get_Count") or 0) or 0
    for i = 0, count - 1, 1 do
        position = get_position(call_method(players, "get_Item", i))
        if position ~= nil then
            return position
        end
    end

    return nil
end

local function get_camera()
    return safe_call(function()
        return sdk.get_primary_camera()
    end)
end

local function get_camera_transform(camera)
    if camera == nil then
        return nil
    end

    local transform = call_method(camera, "get_Transform") or
        call_method(camera, "get_transform")
    if transform ~= nil then
        return transform
    end

    local game_object = call_method(camera, "get_GameObject") or
        call_method(camera, "get_GameObjectRef")
    if game_object ~= nil then
        if get_transform_method ~= nil then
            transform = safe_call(function()
                return get_transform_method:call(game_object)
            end)
            if transform ~= nil then
                return transform
            end
        end

        transform = call_method(game_object, "get_Transform") or
            call_method(game_object, "get_transform")
        if transform ~= nil then
            return transform
        end
    end

    return nil
end

local function get_camera_position()
    local camera = get_camera()
    local transform = get_camera_transform(camera)
    if transform == nil then
        return nil
    end

    local position = read_vector3(safe_call(function()
        return transform:get_position()
    end))
    if position ~= nil then
        return position
    end

    return read_vector3(call_method(transform, "get_Position") or call_method(transform, "get_WorldPosition"))
end

local function set_transform_position(transform, position)
    if transform == nil or position == nil then
        return false
    end

    local ok = pcall(function()
        transform:set_position(Vector4f.new(position.x, position.y, position.z, 1.0), true)
    end)
    if ok then
        return true
    end

    ok = try_call_method(transform, "set_Position", Vector3f.new(position.x, position.y, position.z))
    if ok then
        return true
    end

    ok = try_call_method(transform, "set_WorldPosition", Vector3f.new(position.x, position.y, position.z))
    return ok
end

local function look_rotation(position, target)
    if position == nil or target == nil then
        return nil
    end

    local direction = normalize_vec3({
        x = target.x - position.x,
        y = target.y - position.y,
        z = target.z - position.z
    })

    if settings.invert_look_direction then
        direction.x = -direction.x
        direction.y = -direction.y
        direction.z = -direction.z
    end

    return safe_call(function()
        return Vector3f.new(direction.x, direction.y, direction.z):to_quat()
    end)
end

local function set_transform_rotation(transform, rotation)
    if transform == nil or rotation == nil then
        return false
    end

    local ok = pcall(function()
        transform:set_rotation(rotation)
    end)
    if ok then
        return true
    end

    ok = try_call_method(transform, "set_Rotation", rotation)
    return ok
end

local function get_camera_fov(camera)
    if camera == nil then
        return nil
    end

    local methods = {
        "get_Fov",
        "get_FOV",
        "get_FieldOfView",
        "get_FovY",
        "get_fov"
    }

    for _, method_name in ipairs(methods) do
        local value = tonumber(call_method(camera, method_name))
        if value ~= nil then
            return value
        end
    end

    return nil
end

local function set_camera_fov(camera, value)
    if camera == nil or value == nil or not settings.apply_fov then
        return false
    end

    local methods = {
        "set_Fov",
        "set_FOV",
        "set_FieldOfView",
        "set_FovY",
        "set_fov"
    }

    for _, method_name in ipairs(methods) do
        local ok = try_call_method(camera, method_name, value)
        if ok then
            return true
        end
    end

    return false
end

local function get_current_target()
    if settings.use_player_as_target then
        local player_position = get_player_position()
        if player_position ~= nil then
            return {
                x = player_position.x + settings.target_x,
                y = player_position.y + settings.target_offset_y + settings.target_y,
                z = player_position.z + settings.target_z
            }
        end
    end

    return vec3(settings.target_x, settings.target_y, settings.target_z)
end

local function capture_target_from_player()
    local player_position = get_player_position()
    if player_position == nil then
        notify("Could not find player")
        return
    end

    settings.target_x = player_position.x
    settings.target_y = player_position.y + settings.target_offset_y
    settings.target_z = player_position.z
    settings.use_player_as_target = false
    save_settings()
    notify("Locked target from player")
end

local function build_crane_desired()
    local target = get_current_target()
    if target == nil then
        return nil, nil, nil
    end

    local dt = 1.0 / 60.0
    settings.yaw_degrees = settings.yaw_degrees + ((tonumber(settings.orbit_speed_degrees) or 0.0) * dt)
    settings.height = clamp(
        settings.height + ((tonumber(settings.crane_speed) or 0.0) * dt),
        settings.min_height,
        settings.max_height
    )
    settings.radius = clamp(
        settings.radius + ((tonumber(settings.dolly_speed) or 0.0) * dt),
        settings.min_radius,
        settings.max_radius
    )

    local yaw = deg_to_rad(settings.yaw_degrees)
    local position = {
        x = target.x + (math.sin(yaw) * settings.radius),
        y = target.y + settings.height,
        z = target.z + (math.cos(yaw) * settings.radius)
    }

    return position, target, settings.fov
end

local function frame_duration(frame)
    if frame == nil then
        return math.max(0.1, tonumber(settings.captured_keyframe_duration) or 3.0)
    end
    return math.max(0.1, tonumber(frame.duration) or tonumber(settings.captured_keyframe_duration) or 3.0)
end

local function build_keyframe_desired()
    if #settings.keyframes < 2 then
        state.playing = false
        state.status = "Need at least two keyframes"
        return nil, nil, nil
    end

    if not state.playing then
        local frame = settings.keyframes[state.play_index] or settings.keyframes[1]
        return copy_vec3(frame.position), copy_vec3(frame.target), tonumber(frame.fov) or settings.fov
    end

    local dt = (1.0 / 60.0) * math.max(0.01, tonumber(settings.playback_speed) or 1.0)
    local from_index = state.play_index
    local to_index = from_index + 1
    if to_index > #settings.keyframes then
        if settings.loop_keyframes then
            to_index = 1
        else
            state.playing = false
            state.play_index = #settings.keyframes - 1
            state.play_time = frame_duration(settings.keyframes[#settings.keyframes])
            notify("Playback finished")
            local frame = settings.keyframes[#settings.keyframes]
            return copy_vec3(frame.position), copy_vec3(frame.target), tonumber(frame.fov) or settings.fov
        end
    end

    local from_frame = settings.keyframes[from_index]
    local to_frame = settings.keyframes[to_index]
    local duration = frame_duration(to_frame)

    state.play_time = state.play_time + dt
    while state.play_time > duration and state.playing do
        state.play_time = state.play_time - duration
        state.play_index = state.play_index + 1
        if state.play_index >= #settings.keyframes then
            if settings.loop_keyframes then
                state.play_index = 1
            else
                state.playing = false
                state.play_index = #settings.keyframes - 1
                state.play_time = frame_duration(settings.keyframes[#settings.keyframes])
            end
        end

        from_index = state.play_index
        to_index = from_index + 1
        if to_index > #settings.keyframes then
            to_index = 1
        end
        from_frame = settings.keyframes[from_index]
        to_frame = settings.keyframes[to_index]
        duration = frame_duration(to_frame)
    end

    local t = smoothstep(state.play_time / duration)
    local position = lerp_vec3(from_frame.position, to_frame.position, t)
    local target = lerp_vec3(from_frame.target, to_frame.target, t)
    local fov = lerp_number(tonumber(from_frame.fov) or settings.fov, tonumber(to_frame.fov) or settings.fov, t)
    state.status = string.format("Playing %d -> %d", from_index, to_index)
    return position, target, fov
end

local function build_desired_camera()
    if settings.mode == MODE_KEYFRAMES then
        return build_keyframe_desired()
    end

    return build_crane_desired()
end

local function apply_camera()
    state.frame = state.frame + 1

    if not settings.enabled then
        state.status = "Disabled"
        return
    end

    init_types()

    local desired_position, desired_target, desired_fov = build_desired_camera()
    if desired_position == nil or desired_target == nil then
        state.status = "Waiting for target"
        return
    end

    state.desired_position = desired_position
    state.desired_target = desired_target

    local position_alpha = exp_smooth_alpha(settings.position_smoothness)
    local target_alpha = exp_smooth_alpha(settings.target_smoothness)
    local fov_alpha = exp_smooth_alpha(settings.fov_smoothness)

    state.smoothed_position = lerp_vec3(state.smoothed_position or desired_position, desired_position, position_alpha)
    state.smoothed_target = lerp_vec3(state.smoothed_target or desired_target, desired_target, target_alpha)
    state.smoothed_fov = lerp_number(state.smoothed_fov or desired_fov, desired_fov, fov_alpha)

    local camera = get_camera()
    local transform = get_camera_transform(camera)
    if camera == nil or transform == nil then
        state.status = "Could not find primary camera"
        return
    end

    local rotation = look_rotation(state.smoothed_position, state.smoothed_target)
    if rotation == nil then
        state.status = "Could not build look rotation"
        return
    end

    local position_ok = set_transform_position(transform, state.smoothed_position)
    local rotation_ok = set_transform_rotation(transform, rotation)
    local fov_ok = set_camera_fov(camera, state.smoothed_fov)

    if position_ok and rotation_ok then
        if settings.apply_fov then
            state.status = fov_ok and "Camera active" or "Camera active, FOV unsupported"
        elseif state.status == "Disabled" or state.status == "Waiting for target" or state.status == "Could not find primary camera" then
            state.status = "Camera active"
        end
    else
        state.status = "Could not write camera transform"
    end
end

local function reset_smoothing()
    state.smoothed_position = nil
    state.smoothed_target = nil
    state.smoothed_fov = nil
end

local function capture_keyframe()
    local target = state.desired_target or get_current_target()
    if target == nil then
        notify("No target to capture")
        return
    end

    local position = state.desired_position or get_camera_position()
    if position == nil then
        notify("No camera position to capture")
        return
    end

    table.insert(settings.keyframes, {
        position = copy_vec3(position),
        target = copy_vec3(target),
        fov = tonumber(state.smoothed_fov) or tonumber(get_camera_fov(get_camera())) or settings.fov,
        duration = math.max(0.1, tonumber(settings.captured_keyframe_duration) or 3.0)
    })

    save_settings()
    notify("Captured keyframe " .. tostring(#settings.keyframes))
end

local function remove_last_keyframe()
    if #settings.keyframes == 0 then
        notify("No keyframes")
        return
    end

    table.remove(settings.keyframes)
    if state.play_index > #settings.keyframes then
        state.play_index = math.max(1, #settings.keyframes - 1)
    end
    save_settings()
    notify("Removed last keyframe")
end

local function start_playback()
    if #settings.keyframes < 2 then
        notify("Need at least two keyframes")
        return
    end

    settings.mode = MODE_KEYFRAMES
    settings.enabled = true
    state.playing = true
    state.play_index = 1
    state.play_time = 0.0
    reset_smoothing()
    save_settings()
    notify("Playback started")
end

local function stop_playback()
    state.playing = false
    notify("Playback stopped")
end

local function clear_keyframes()
    settings.keyframes = {}
    state.playing = false
    state.play_index = 1
    state.play_time = 0.0
    save_settings()
    notify("Cleared keyframes")
end

local function draw_notification()
    if state.notification_frames <= 0 or state.notification == "" then
        return
    end

    draw.filled_rect(80, 80, 640, 42, 0xCC1F1F1F)
    draw.text(state.notification, 100, 92, 0xFFFFFFFF)
    state.notification_frames = state.notification_frames - 1
end

local function draw_status()
    if not settings.draw_status or not settings.enabled then
        return
    end

    draw.filled_rect(24, 24, 390, 70, 0x99111111)
    draw.text("Cinematic Camera: " .. tostring(state.status), 40, 38, 0xFFFFFFFF)
    draw.text(string.format("Mode: %s | Keys: %d", MODES[settings.mode] or "Unknown", #settings.keyframes), 40, 62, 0xFFCFCFCF)
end

local function draw_crane_ui()
    local changed = false
    local item_changed = false

    item_changed, settings.use_player_as_target = imgui.checkbox("Follow player target", settings.use_player_as_target)
    changed = changed or item_changed

    if imgui.button("Lock Target From Player") then
        capture_target_from_player()
    end

    item_changed, settings.target_x = imgui.drag_float("Target X / Offset X", settings.target_x, 0.05, -100000.0, 100000.0)
    changed = changed or item_changed
    item_changed, settings.target_y = imgui.drag_float("Target Y / Offset Y", settings.target_y, 0.05, -100000.0, 100000.0)
    changed = changed or item_changed
    item_changed, settings.target_z = imgui.drag_float("Target Z / Offset Z", settings.target_z, 0.05, -100000.0, 100000.0)
    changed = changed or item_changed
    item_changed, settings.target_offset_y = imgui.drag_float("Player head offset", settings.target_offset_y, 0.05, -2.0, 4.0)
    changed = changed or item_changed

    imgui.separator()
    item_changed, settings.radius = imgui.drag_float("Arm radius", settings.radius, 0.05, settings.min_radius, settings.max_radius)
    changed = changed or item_changed
    item_changed, settings.height = imgui.drag_float("Arm height", settings.height, 0.05, settings.min_height, settings.max_height)
    changed = changed or item_changed
    item_changed, settings.yaw_degrees = imgui.drag_float("Orbit yaw", settings.yaw_degrees, 0.1, -720.0, 720.0)
    changed = changed or item_changed
    item_changed, settings.orbit_speed_degrees = imgui.drag_float("Orbit speed deg/s", settings.orbit_speed_degrees, 0.05, -60.0, 60.0)
    changed = changed or item_changed
    item_changed, settings.crane_speed = imgui.drag_float("Crane speed", settings.crane_speed, 0.02, -8.0, 8.0)
    changed = changed or item_changed
    item_changed, settings.dolly_speed = imgui.drag_float("Dolly speed", settings.dolly_speed, 0.02, -8.0, 8.0)
    changed = changed or item_changed

    if changed then
        save_settings()
    end
end

local function draw_smoothing_ui()
    local changed = false
    local item_changed = false

    item_changed, settings.position_smoothness = imgui.drag_float("Position smoothing", settings.position_smoothness, 0.05, 0.0, 20.0)
    changed = changed or item_changed
    item_changed, settings.target_smoothness = imgui.drag_float("Look smoothing", settings.target_smoothness, 0.05, 0.0, 20.0)
    changed = changed or item_changed
    item_changed, settings.invert_look_direction = imgui.checkbox("Invert look direction", settings.invert_look_direction)
    changed = changed or item_changed

    item_changed, settings.apply_fov = imgui.checkbox("Apply FOV", settings.apply_fov)
    changed = changed or item_changed
    item_changed, settings.fov = imgui.drag_float("FOV", settings.fov, 0.1, 1.0, 160.0)
    changed = changed or item_changed
    item_changed, settings.fov_smoothness = imgui.drag_float("FOV smoothing", settings.fov_smoothness, 0.05, 0.0, 20.0)
    changed = changed or item_changed
    item_changed, settings.draw_status = imgui.checkbox("Draw status overlay", settings.draw_status)
    changed = changed or item_changed

    if imgui.button("Reset Smoothing") then
        reset_smoothing()
    end

    if changed then
        save_settings()
    end
end

local function draw_keyframe_ui()
    local changed = false
    local item_changed = false

    item_changed, settings.captured_keyframe_duration = imgui.drag_float("New key duration", settings.captured_keyframe_duration, 0.05, 0.1, 30.0)
    changed = changed or item_changed
    item_changed, settings.playback_speed = imgui.drag_float("Playback speed", settings.playback_speed, 0.02, 0.05, 5.0)
    changed = changed or item_changed
    item_changed, settings.loop_keyframes = imgui.checkbox("Loop keyframes", settings.loop_keyframes)
    changed = changed or item_changed

    if imgui.button("Capture Keyframe") then
        capture_keyframe()
    end
    imgui.same_line()
    if imgui.button("Remove Last") then
        remove_last_keyframe()
    end

    if imgui.button("Play Rail") then
        start_playback()
    end
    imgui.same_line()
    if imgui.button("Stop Rail") then
        stop_playback()
    end
    imgui.same_line()
    if imgui.button("Clear Rail") then
        clear_keyframes()
    end

    imgui.text("Keyframes: " .. tostring(#settings.keyframes))
    if imgui.tree_node("Keyframe List") then
        local any_frame_changed = false
        for i, frame in ipairs(settings.keyframes) do
            local label = string.format(
                "%02d P(%.2f, %.2f, %.2f) T(%.2f, %.2f, %.2f)",
                i,
                frame.position.x,
                frame.position.y,
                frame.position.z,
                frame.target.x,
                frame.target.y,
                frame.target.z
            )
            imgui.text(label)
            local duration_changed = false
            duration_changed, frame.duration = imgui.drag_float("Duration##key_" .. tostring(i), frame_duration(frame), 0.05, 0.1, 30.0)
            any_frame_changed = any_frame_changed or duration_changed
        end
        imgui.tree_pop()

        if any_frame_changed then
            save_settings()
        end
    end

    if changed then
        save_settings()
    end
end

local function draw_main_ui()
    local changed = false
    local item_changed = false

    item_changed, settings.enabled = imgui.checkbox("Enabled", settings.enabled)
    changed = changed or item_changed

    item_changed, settings.mode = imgui.combo("Mode", settings.mode, MODES)
    changed = changed or item_changed

    if imgui.button("Save Settings") then
        save_settings()
        notify("Settings saved")
    end
    imgui.same_line()
    if imgui.button("Reload Settings") then
        load_settings()
        reset_smoothing()
    end

    imgui.separator()
    if imgui.tree_node("Crane Rig") then
        draw_crane_ui()
        imgui.tree_pop()
    end

    if imgui.tree_node("Smoothing / Lens") then
        draw_smoothing_ui()
        imgui.tree_pop()
    end

    if imgui.tree_node("Keyframe Rail") then
        draw_keyframe_ui()
        imgui.tree_pop()
    end

    imgui.separator()
    imgui.text("Status: " .. tostring(state.status))
    imgui.text("Playback: " .. (state.playing and "Playing" or "Stopped"))

    if changed then
        save_settings()
        reset_smoothing()
    end
end

load_settings()

re.on_application_entry("LockScene", function()
    apply_camera()
end)

re.on_frame(function()
    draw_notification()
    draw_status()
end)

re.on_draw_ui(function()
    if imgui.tree_node("RE4R Cinematic Camera Rig") then
        draw_main_ui()
        imgui.tree_pop()
    end
end)
