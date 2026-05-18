import PropTypes from 'prop-types';
import React from 'react';
import Label from 'Components/Label';
import { kinds, sizes } from 'Helpers/Props';

// Renders one narrator as a Label chip. Phase 12.2 surface — Phase 12.4
// will wrap this in a <Link to={`/narrator/${id}`}> once the per-narrator
// detail page lands. Until then chips are intentionally non-clickable so
// the UI doesn't promise a route that 404s.
function NarratorChip({ name }) {
  return (
    <Label
      kind={kinds.INFO}
      size={sizes.SMALL}
    >
      {name}
    </Label>
  );
}

NarratorChip.propTypes = {
  id: PropTypes.number,
  name: PropTypes.string.isRequired
};

export default NarratorChip;
