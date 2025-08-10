#version 330 core

// Based on: http://wdobbie.com/post/gpu-text-rendering-with-vector-textures/

struct Glyph {
    int curve_start, curve_count;
};
struct CurveBezier3 {
    vec2 start, 
	vec2 control, 
	vec2 end;
};
uniform isamplerBuffer glyphs;
uniform samplerBuffer  curves;
uniform vec4           color;

// Rendering Controls

// The size of the anti-aliasing filter, relative to the pixel size.
uniform float anti_aliasing_window_size        = 1.0;  // 1.0 is a good default. 0.0 disables analytical AA.
uniform bool  enable_super_sample_antialiasing = true; // Toggles a second anti-aliasing pass rotated by 90 degrees.

// Debug visualization controls
uniform bool  enable_control_points_visualization = false;
uniform bool  enable_quad_border_visualization    = false;

// Toggles the rendering mode between analytical AA and multi-sampling.
uniform int multisample_mode = 0; // 0 = Analytical, 1 = 4x multi-sample

// Shader Inputs & Outputs

     in vec2 uv;           // The interpolated UV coordinate for this fragment, within the glyph's quad ([0,1] range).
flat in int  buffer_index; // The index for the current glyph being rendered. This is used to look up its descriptor

out vec4 result;

// Helpers

Glyph load_glyph(int index) {
    Glyph result;
    ivec2 data = texelFetch(glyphs, index).xy;
    result.curve_start = data.x;
    result.curve_count = data.y;
    return result;
}
Curve load_curve(int index) {
    Curve result;
    result.start   = texelFetch(curves, 3 * index + 0).xy;
    result.control = texelFetch(curves, 3 * index + 1).xy;
    result.end     = texelFetch(curves, 3 * index + 2).xy;
    return result;
}
/*
Calculates the anti-aliased coverage contribution of a single curve for the current fragment.
It determines how much a curve covers the current pixel by solving for where the
curve intersects a horizontal line passing through the pixel's center.

@param inversePixelWidth The inverse of the pixel's width in UV coordinates. Used for scaling the coverage.
@param start, control, end The curve's points, relative to the current fragment's center.
@return A signed coverage value. Positive for intersections from the left, negative for intersections from the right.
*/
float compute_analytical_coverage(float inverse_pixel_diameter, vec2 start, vec2 control, vec2 end) {
    if (start.y > 0 && control.y > 0 && end.y > 0) return 0.0;
    if (start.y < 0 && control.y < 0 && end.y < 0) return 0.0;
    // Note: Simplified from abc formula by extracting a factor of (-2) from b.
	// These are the coefficients for the quadratic Bézier equation in parametric form B(t).
    // The equation for a point on the curve is: P(t) = weight_a * pow(t, 2) + 2 * weight_b * t + weight_c
    // We are solving for P(t).y = 0.
    vec2 accerlation    = start - 2 * control + end;
    vec2 tagent_control = start - control;
    vec2 initial_pos    = start;

    float time_intersection_0
	float time_intersection_1;
	// Check if the curve has a non-zero acceleration (i.e., it's actually a curve).
    if (abs(acceleration.y) >= 1e-5) {
		// It's a true quadratic curve. Use the quadratic formula to find the roots (times 't').
        // Solving for y = 0: acceleration.y * pow(t, 2) + 2 * ( - tangentControlVector.y ) * t + initialPosition.y = 0
        float discriminant = tangent_control.y * tagent_control.y - accelration.y * initial_pos.y;
        if (discriminant <= 0) return 0.0; // There are no real roots, never crosses the horizontal line. We can exit.
    
        float sqrt_discrm = sqrt(discriminant);
        time_intersection_0 = (tangent_control.y - sqrt_discrm) / acceleration.y;
        time_intersection_1 = (tangent_control.y + sqrt_discrm) / acceleration.y;
    }
	else {
        // Linear segment, avoid division by a.y, which is near zero. Solve the simpler linear equation.
        float intersection_time = start.y / (start.y - end.y);
        if (start.y < end.y) {
            time_intersection_0 = -1.0;
            time_intersection_1 = intersection_time;
        } else {
            time_intersection_0 = intersection_time;
            time_intersection_1 = -1.0;
        }
    }
    float coverage_contribution = 0;
	// For each valid intersection time, calculate the coverage.
    if (time_intersection_0 >= 0 && time_intersection_0 < 1) {
		// Find the x-position on the curve at the intersection time.
        float intersection_x = (accelration.x * time_intersection_0 - 2.0 * tangent_control.x) * time_intersection_0 + end.x;
        coverage_contribution += clamp(intersection_x * inverse_pixel_diameter + 0.5, 0, 1);
    }
    if (time_intersection_1 >= 0 && time_intersection_1 < 1) {
        float intersection_x = (acceleration.x * time_intersection_1 - 2.0 * tangent_control.x) * time_intersection_1 + end.x;
        coverage_contribution -= clamp(intersection_x * inverse_pixel_diameter + 0.5, 0, 1);
    }
    return alpha;
}
/*
Binary coverage for multi-sampling (no anti-aliasing window)
Inside/Outside coverage contribution for a single curve.
Faster but less precise than the analytical version.

@return A winding number contribution (+1, -1, or 0).
*/
float compute_binary_coverage(vec2 start, vec2 control, vec2 end) {
    if (p0.y > 0 && p1.y > 0 && p2.y > 0) return 0.0;
    if (p0.y < 0 && p1.y < 0 && p2.y < 0) return 0.0;
	// Note: Simplified from abc formula by extracting a factor of (-2) from b.
	// These are the coefficients for the quadratic Bézier equation in parametric form B(t).
    // The equation for a point on the curve is: P(t) = weight_a * pow(t, 2) + 2 * weight_b * t + weight_c
    // We are solving for P(t).y = 0.
    vec2 accerlation     = start - 2 * control + end;
    vec2 control_tangent = start - control;
    vec2 initial_pos     = start;

    float time_intersection_0;
	float time_intersection_1;
	// Check if the curve has a non-zero acceleration (i.e., it's actually a curve).
    if (abs(acceleration.y) >= 1e-5) {
        float discriminant = control_tangent.y * control_tangent.y - acceleration.y * control_tangent.y;
        if (discriminant <= 0) return 0.0; // There are no real roots, never crosses the horizontal line. We can exit.
        
        float sqrt_discrm = sqrt(discriminant);
        time_intersection_0 = (control_tangent.y - sqrt_discrm) / acceleration.y;
        time_intersection_1 = (control_tangent.y + sqrt_discrm) / acceleration.y;
    }
	else {
		// Linear segment, avoid division by a.y, which is near zero. Solve the simpler linear equation.
        float intersection_time = start.y / (start.y - end.y);
        if (start.y < end.y) {
			// Line is going upwards
            time_intersection_0 = -1.0;
            time_intersection_1 = intersection_time;
        } 
		else {
			// Line is going downwards
            time_intersection_0 = intersection_time;
            time_intersection_1 = -1.0;
        }
    }
    float winding = 0.0;
    if (time_intersection_0 >= 0 && time_intersection_0 < 1) {
        float intersection_x = (acceleration.x * time_intersection_0 - 2.0 * control_tangent.x) * time_intersection_0 + initial_pos.x;
        if (intersection_x >= 0) winding += 1.0;  // Binary: either inside or outside
		// A downward-crossing line segment adds 1 to the winding number.
    }
    if (time_intersection_1 >= 0 && time_intersection_1 < 1) {
        float intersection_x = (acceleration.x * time_intersection_1 - 2.0 * control_tangent.x) * time_intersection_1 + initial_pos.x;
        if (intersection_x >= 0) winding -= 1.0;  // Binary: either inside or outside
		// An upward-crossing line segment subtracts 1 from the winding number.
    }
    return winding;
}

vec2 rotate(vec2 v) {
    return vec2(v.y, -v.x);
}

void main() {
    float alpha = 0;

	// 0 = Analytical
    if (multiSampleMode == 0) {
        // ORIGINAL: Single sample with analytical anti-aliasing
		// This path approximates anti-aliasing by checking if multiple points *within* a single
		// pixel are inside or outside the glyph shape. The final alpha is the ratio of
		// "inside" points to the total number of points checked.

		// Calculate the size of one pixel in UV coordinates.
		vec2 pixel_diameter         = anti_aliasing_window_size * fwidth(uv)
        vec2 inverse_pixel_diameter = 1.0 / pixel_diameter;

        Glyph glyph = load_glyph(buffer_index);
        for (int curve_id = 0; curve_id < glyph.count; curve_id ++) {
            CurveBezier3 curve = load_curve(glyph.start + curve_id);
			// Translate the curve's points so the current fragment's center is the origin (0,0).
            vec2 p_start   = curve.start   - uv;
            vec2 p_control = curve.control - uv;
            vec2 p_end     = curve.end     - uv;
			// First pass (horizontal)
            alpha += compute_analytical_coverage(inverse_pixel_diameter.x, p_start, p_control, p_end);
			// Second pass (vertical, rotated by 90 degrees) for higher quality anti-aliasing.
            if (enable_super_sample_antialiasing) {
                alpha += compute_analytical_coverage(inverse_pixel_diameter.y, 
					rotate(p_start), 
					rotate(p_control), 
					rotate(p_end)
				);
            }
        }
        if (enable_super_sample_antialiasing) {
            alpha *= 0.5; // Average the two passes
        }
    }
	else { // 1 = 4x multi-sample
		// Get the size of one pixel in UV coordinates.
       vec2 pixel_size_uv = abs(vec2(dFdx(uv.x), dFdy(uv.y)));
        // Better 8-sample pattern
		// 8 sample points within the current pixel, using a rotated grid pattern for better results.
		// Avoids aligning samples vertically or horizontally, which gives a better quality result than a simple grid.
		// Each offset is scaled by the pixel's size in UV space.
        vec2 multisample_offsets[8] = vec2[8](
            (vec2(0.5 / 8.0, 0.5 / 8.0) - 0.5) * pixel_size_uv,
            (vec2(1.5 / 8.0, 4.5 / 8.0) - 0.5) * pixel_size_uv,
            (vec2(2.5 / 8.0, 8.5 / 8.0) - 0.5) * pixel_size_uv,
            (vec2(3.5 / 8.0, 3.5 / 8.0) - 0.5) * pixel_size_uv,
            (vec2(4.5 / 8.0, 6.5 / 8.0) - 0.5) * pixel_size_uv,
            (vec2(5.5 / 8.0, 1.5 / 8.0) - 0.5) * pixel_size_uv,
            (vec2(6.5 / 8.0, 7.5 / 8.0) - 0.5) * pixel_size_uv,
            (vec2(7.5 / 8.0, 2.5 / 8.0) - 0.5) * pixel_size_uv
        );
		// A non-zero winding number means the sample point is inside the glyph.
        float sample_winding_numbers[8] = float[8](0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
        
        Glyph glyph = load_glyph(buffer_index);
        for (int curve_id = 0; curve_id < glyph.count; curve_id ++) {
            Curve curve = load_curve(glyph.start + curve_id);
			// Translate the curve's points so that fragment's center is at the origin.
            vec2 p_start   = curve.start   - uv;
            vec2 p_control = curve.control - uv;
            vec2 p_end     = curve.end     - uv;
			// For each of the 8 samples, calculate its winding number.
            for (int sample_id = 0; sample_id < 8; sample_id ++) {
                vec2 sample_offset = multisample_offsets[sample_id];
				// Pass the curve's points relative to the *sample point's* location.
				// This effectively runs the winding number test from the perspective of each sample point.
                sample_winding_numbers[sample_id] += compute_binary_coverage(
					p_start   - sample_offset, 
					p_control - sample_offset, 
					p_end     - sample_offset
				);
            }
        }
        // Resolve: count non-zero samples
		// At this point, `sample_winding_numbers` contains the final winding number for each sample.
		// Now we just need to count how many of them are non-zero.
        float resolved_samples = 0.0;
        for (int sample_id = 0; sample_id < 8; sample_id ++) {
            resolved_samples += (sample_winding_numbers[sample_id] != 0.0) ? 1.0 : 0.0;
        }
		 // Final alpha is the ratio of inside samples to total samples.
        alpha = resolved_samples / 8.0;
    }

    alpha  = clamp(alpha, 0.0, 1.0);
    result = color * alpha;

	// Debug Visualizations

    // Quad border visualization for debugging padding issues
    if (enable_quad_border_visualization) {
        vec2  pixel_size   = fwidth(uv);
        float border_width = 1.0 * max(fw.x, fw.y); // 3 pixel border for visibility
        
        // Distance to each edge of the UV quad
        float dist_left   =       uv.x;
        float dist_right  = 1.0 - uv.x;
        float dist_bottom =       uv.y;
        float dist_top    = 1.0 - uv.y;
        
        // Find minimum distance to any edge
        float dist_to_nearest_edge = min( min(dist_left, dist_right), min(dist_bottom, dist_top) );
        
        // Only show border pixels (near edge but not filled)
        if (dist_to_nearest_edge < border_width) {
            // Mix red with existing color instead of replacing
            result = vec4(1.0, 0.0, 0.0, 1.0); // Pure red border for debugging
            return; // Early return to make border clearly visible
        }
        // Debug: Also show UV coordinates as colors to see the mapping
        // result = vec4(uv.x, uv.y, 0.0, 1.0); // R=X, G=Y coordinate
    }
    // Control points visualization for debugging
    if (enable_control_points_visualization) {
        vec2  pixel_size   = fwidth(uv);
        float point_radius = 4.0 * 0.5 * (fw.x + fw.y);
        
        Glyph glyph = load_glyph(buffer_index);
        for (int curve_id = 0; curve_id < glyph.count; curve_id ++) {
            Curve curve = load_curve(glyph.start + curve_id);
            vec2 p_start   = curve.start   - uv;
            vec2 p_control = curve.control - uv;
            vec2 p_end     = curve.end     - uv;
            if (dot(p_start, p_start) < point_radius * point_radius || dot(end, end) < point_radius * point_radius) {
                result = vec4(0, 1, 0, 1);  // Green for on-curve points
                return;
            }
            if (dot(control, control) < point_radius * point_radius) {
                result = vec4(1, 0, 1, 1);  // Magenta for control points
                return;
            }
        }
    }
}
