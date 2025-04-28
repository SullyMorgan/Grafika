#version 330 core
out vec4 FragColor;

uniform vec3 uLightColor;
uniform vec3 uLightPos;
uniform vec3 uViewPos;

uniform float uShininess;

uniform vec3 ambientStrength;
uniform vec3 diffuseStrength;
uniform vec3 specularStrength;
uniform vec4 FaceColor;
		
in vec4 outCol;
in vec3 outNormal;
in vec3 outWorldPosition;

void main()
{
    vec3 norm = normalize(outNormal);
	vec3 lightDir = normalize(uLightPos - outWorldPosition);
	vec3 viewDir = normalize(uViewPos - outWorldPosition);
	vec3 reflectDir = reflect(-lightDir, norm);

	float diff = max(dot(norm, lightDir), 0.0);
	float spec = pow(max(dot(viewDir, reflectDir), 0.0), uShininess);

	vec3 ambient = ambientStrength * uLightColor;
	vec3 diffuse = diffuseStrength * diff * uLightColor;
	vec3 specular = specularStrength * spec * uLightColor;

	vec3 lighting = ambient + diffuse + specular;
	vec3 result = lighting * FaceColor.rgb;

	FragColor = vec4(result, FaceColor.a);
}