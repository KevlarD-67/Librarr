import PropTypes from 'prop-types';
import React, { Component } from 'react';
import FormInputGroup from 'Components/Form/FormInputGroup';
import Icon from 'Components/Icon';
import SpinnerIcon from 'Components/SpinnerIcon';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import TableRow from 'Components/Table/TableRow';
import { icons, inputTypes, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './LibraryImportRow.css';

class LibraryImportRow extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      id,
      term,
      isPopulated,
      isFetching
    } = this.props;

    if (!isPopulated && !isFetching) {
      this.props.onQueueLookup({ id, term });
    }
  }

  //
  // Listeners

  onTermChange = ({ value }) => {
    const { id } = this.props;

    this.props.onSetValue({ id, term: value });
  };

  onSearchPress = () => {
    const { id, term } = this.props;

    this.props.onQueueLookup({ id, term });
  };

  onAuthorSelect = ({ value }) => {
    const { id, items } = this.props;

    this.props.onSetValue({
      id,
      selectedAuthor: items.find((author) => author.foreignAuthorId === value)
    });
  };

  //
  // Render

  render() {
    const {
      id,
      name,
      term,
      isFetching,
      isPopulated,
      error,
      items,
      selectedAuthor,
      isSelected,
      onSelectedChange
    } = this.props;

    // OpenLibrary routinely returns several records for the same author, and
    // often none of them carry a disambiguation — a search for "Brandon
    // Sanderson" comes back as two records identical in every field except the
    // OL id. Falling back to the id keeps the dropdown from offering choices
    // the user has no way to tell apart. It's only shown on a collision,
    // since it is noise the rest of the time.
    const nameCounts = items.reduce((acc, author) => {
      acc[author.authorName] = (acc[author.authorName] || 0) + 1;

      return acc;
    }, {});

    const authorOptions = items.map((author) => {
      let value = author.authorName;

      if (author.disambiguation) {
        value = `${value} (${author.disambiguation})`;
      } else if (nameCounts[author.authorName] > 1) {
        value = `${value} (${author.foreignAuthorId})`;
      }

      return {
        key: author.foreignAuthorId,
        value
      };
    });

    return (
      <TableRow>
        <TableSelectCell
          id={id}
          isSelected={isSelected}
          isDisabled={!selectedAuthor}
          onSelectedChange={onSelectedChange}
        />

        <TableRowCell className={styles.folder}>
          {name}
        </TableRowCell>

        <TableRowCell className={styles.term}>
          <FormInputGroup
            type={inputTypes.TEXT}
            name="term"
            value={term}
            buttons={[
              <div
                key="search"
                className={styles.searchButton}
                onClick={this.onSearchPress}
                role="button"
                tabIndex={0}
                onKeyPress={this.onSearchPress}
              >
                <Icon name={icons.SEARCH} />
              </div>
            ]}
            onChange={this.onTermChange}
          />
        </TableRowCell>

        <TableRowCell className={styles.author}>
          {
            isFetching &&
              <SpinnerIcon
                name={icons.SPINNER}
                isSpinning={true}
              />
          }

          {
            !isFetching && !!error &&
              <Icon
                name={icons.WARNING}
                kind={kinds.WARNING}
                title={translate('LibraryImportLookupFailed')}
              />
          }

          {
            !isFetching && !error && isPopulated && !items.length &&
              <span className={styles.noMatches}>
                {translate('LibraryImportNoMatches')}
              </span>
          }

          {
            !isFetching && !error && !!items.length &&
              <FormInputGroup
                type={inputTypes.SELECT}
                name="selectedAuthor"
                value={selectedAuthor ? selectedAuthor.foreignAuthorId : ''}
                values={authorOptions}
                onChange={this.onAuthorSelect}
              />
          }
        </TableRowCell>
      </TableRow>
    );
  }
}

LibraryImportRow.propTypes = {
  id: PropTypes.string.isRequired,
  name: PropTypes.string.isRequired,
  term: PropTypes.string.isRequired,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  selectedAuthor: PropTypes.object,
  isSelected: PropTypes.bool,
  onSelectedChange: PropTypes.func.isRequired,
  onQueueLookup: PropTypes.func.isRequired,
  onSetValue: PropTypes.func.isRequired
};

export default LibraryImportRow;
