// Summarises ProjectSettings/QualitySettings.asset: every quality level and the settings that matter here.
const fs = require('fs');
const raw = fs.readFileSync('ProjectSettings/QualitySettings.asset', 'utf8').replace(/\r\n/g, '\n');
const cur = (/m_CurrentQuality: (\d+)/.exec(raw) || [])[1];
const levels = raw.split(/\n  - serializedVersion: 2\n/).slice(1);
console.log('current quality index:', cur);
levels.forEach((l, i) => {
  const g = (k) => (new RegExp('\\n    ' + k + ': ([^\\n]+)').exec(l) || [])[1];
  console.log(
    `${i}  ${(g('name') + '        ').slice(0, 12)}` +
    ` pixels=${g('pixelLightCount')} shadows=${g('shadows')} shadowRes=${g('shadowResolution')}` +
    ` cascades=${g('shadowCascades')} shadowDist=${g('shadowDistance')}` +
    ` texQ=${g('textureQuality')} aniso=${g('anisotropicTextures')} aa=${g('antiAliasing')}` +
    ` softPart=${g('softParticles')} softVeg=${g('softVegetation')} reflProbes=${g('realtimeReflectionProbes')}` +
    ` vsync=${g('vSyncCount')} lodBias=${g('lodBias')} partBudget=${g('particleRaycastBudget')}`
  );
});
