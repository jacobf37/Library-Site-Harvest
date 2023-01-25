using Landis.Utilities;
using Landis.Core;
using Landis.Library.Succession;
using System.Collections.Generic;

namespace Landis.Library.SiteHarvest
{
    /// <summary>
    /// An extended base class for text parsers that need to parse basic
    /// harvest parameters -- cohort selectors and species planting list.
    /// </summary>
    public abstract class BasicParameterParser<T>
        : SuccessionParameterParser<T>
    {
        private bool keywordsEnabled;
        private ISpeciesDataset speciesDataset;
        private InputVar<string> speciesName;
        private MultiSpeciesCohortSelector cohortSelector;
        private MultiSpeciesCohortSelector additionalCohortSelector;

        /// <summary>
        /// Line number where each species was found.  Used to check for
        /// duplicate names in a list.
        /// </summary>
        protected Dictionary<string, int> SpeciesLineNumbers { get; private set; }

        //---------------------------------------------------------------------

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="speciesDataset">
        /// The dataset of species to look up species' names in.
        /// </param>
        /// <param name="keywordsEnabled">
        /// Are keywords like "Oldest" and "AllExceptYoungest" accepted?
        /// </param>
        public BasicParameterParser(ISpeciesDataset speciesDataset,
                                    bool keywordsEnabled)
            :base(speciesDataset, keywordsEnabled)
        {
            this.keywordsEnabled = keywordsEnabled;
            this.speciesDataset = speciesDataset;
            this.speciesName = new InputVar<string>("Species");
            this.SpeciesLineNumbers = new Dictionary<string, int>();
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Reads the optional "PreventEstablishment" keyword.
        /// </summary>
        /// <returns>true if keyword was present; false otherwise</returns>
        protected bool ReadPreventEstablishment()
        {
            return ReadOptionalName(ParameterNames.PreventEstablishment);
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Read a species name from the current input line.
        /// </summary>
        /// <exception cref="InputValueException">
        /// Thrown if the species name was previously used on an earlier line in
        /// the input file.
        /// </exception>
        protected ISpecies ReadSpecies(StringReader currentLine)
        {
            ISpecies species = ReadAndValidateSpeciesName(currentLine);
            int lineNumber;
            if (SpeciesLineNumbers.TryGetValue(species.Name, out lineNumber))
                throw new InputValueException(speciesName.Value.String,
                                              "The species {0} was previously used on line {1}",
                                              speciesName.Value.String, lineNumber);
            else
                SpeciesLineNumbers[species.Name] = LineNumber;

            return species;
        }

        //---------------------------------------------------------------------

        /*/// <summary>
        /// Read a species name from the current input line.
        /// </summary>
        /// <exception cref="InputValueException">
        /// Thrown if the species name is not valid.
        /// </exception>
        protected ISpecies ReadAndValidateSpeciesName(StringReader currentLine)
        {
            ReadValue(speciesName, currentLine);
            ISpecies species = speciesDataset[speciesName.Value.Actual];
            if (species == null)
                throw new InputValueException(speciesName.Value.String,
                                              "{0} is not a species name",
                                              speciesName.Value.String);
            return species;
        }
        */
        //---------------------------------------------------------------------

        /// <summary>
        /// Reads a list of species and their cohorts that should be removed.
        /// </summary>
        protected ICohortSelector ReadSpeciesAndCohorts(params string[] names)
        {
            List<string> namesThatFollow;
            if (names == null)
                namesThatFollow = new List<string>();
            else
                namesThatFollow = new List<string>(names);

            SpeciesLineNumbers.Clear();

            //if we are reading a single repeat
            if (names.Length == 3)
            {
                additionalCohortSelector = new MultiSpeciesCohortSelector();
            }
            else
            {
                cohortSelector = new MultiSpeciesCohortSelector();
            }

            while (!AtEndOfInput && !namesThatFollow.Contains(CurrentName))
            {
                StringReader currentLine = new StringReader(CurrentLine);

                // Species name
                ISpecies species = ReadSpecies(currentLine);

                //  Cohort keyword, cohort age or cohort age range
                //  keyword = (All, Youngest, AllExceptYoungest, Oldest,
                //             AllExceptOldest, 1/{N})
                TextReader.SkipWhitespace(currentLine);
                int indexOfDataAfterSpecies = currentLine.Index;
                string word = TextReader.ReadWord(currentLine);
                if (word == "")
                    throw NewParseException("No cohort keyword, age or age range after the species name");

                bool foundKeyword = false;
                if (keywordsEnabled)
                {
                    if (word == "All")
                    {
                        if (names.Length == 3)
                        {
                            additionalCohortSelector[species] = SelectCohorts.All;
                        }
                        else
                        {
                            cohortSelector[species] = SelectCohorts.All;
                        }
                        foundKeyword = true;
                    }
                    else if (word == "Youngest")
                    {
                        if (names.Length == 3)
                        {
                            additionalCohortSelector[species] = SelectCohorts.Youngest;
                        }
                        else
                        {
                            cohortSelector[species] = SelectCohorts.Youngest;
                        }
                        foundKeyword = true;
                    }
                    else if (word == "AllExceptYoungest")
                    {
                        if (names.Length == 3)
                        {
                            additionalCohortSelector[species] = SelectCohorts.AllExceptYoungest;
                        }
                        else
                        {
                            cohortSelector[species] = SelectCohorts.AllExceptYoungest;
                        }
                        foundKeyword = true;
                    }
                    else if (word == "Oldest")
                    {
                        if (names.Length == 3)
                        {
                            additionalCohortSelector[species] = SelectCohorts.Oldest;
                        }
                        else
                        {
                            cohortSelector[species] = SelectCohorts.Oldest;
                        }
                        foundKeyword = true;
                    }
                    else if (word == "AllExceptOldest")
                    {
                        if (names.Length == 3)
                        {
                            additionalCohortSelector[species] = SelectCohorts.AllExceptOldest;
                        }
                        else
                        {
                            cohortSelector[species] = SelectCohorts.AllExceptOldest;
                        }
                        foundKeyword = true;
                    }
                    else if (word.StartsWith("1/"))
                    {
                        InputVar<ushort> N = new InputVar<ushort>("1/N");
                        N.ReadValue(new StringReader(word.Substring(2)));
                        if (N.Value.Actual == 0)
                            throw NewParseException("For \"1/N\", N must be > 0");

                        if (names.Length == 3)
                        {
                            additionalCohortSelector[species] = new EveryNthCohort(N.Value.Actual).SelectCohorts;
                        }
                        else
                        {
                            cohortSelector[species] = new EveryNthCohort(N.Value.Actual).SelectCohorts;
                        }
                        foundKeyword = true;
                    }
                }

                if (foundKeyword)
                    CheckNoDataAfter("the keyword \"" + word + "\"", currentLine);
                else
                {
                    //  Read one or more ages or age ranges
                    List<ushort> ages = new List<ushort>();
                    List<AgeRange> ranges = new List<AgeRange>();
                    currentLine = new StringReader(CurrentLine.Substring(indexOfDataAfterSpecies));
                    InputVar<AgeRange> ageOrRange = new InputVar<AgeRange>("Age or Age Range");
                    while (currentLine.Peek() != -1)
                    {
                        ReadValue(ageOrRange, currentLine);
                        ValidateAgeOrRange(ageOrRange.Value, ages, ranges);
                        TextReader.SkipWhitespace(currentLine);
                    }
                    if (names.Length == 3)
                    {
                        CreateAdditionalCohortSelectionMethodFor(species, ages, ranges);
                    }
                    else
                    {
                        CreateCohortSelectionMethodFor(species, ages, ranges);
                    }
                }

                GetNextLine();
            }

            if (SpeciesLineNumbers.Count == 0)
                throw NewParseException("Expected a line starting with a species name");

            if (names.Length == 3)
            {
                return additionalCohortSelector;
            }
            else
            {
                return cohortSelector;
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Creates and stores the cohort selection method for a particular
        /// species based on lists of specific ages and age ranges.
        /// </summary>
        /// <remarks>
        /// Derived classes can override this method to perform special
        /// handling of ages and ranges (for example, percentages for partial
        /// harvesting in biomass extensions).
        /// </remarks>
        protected virtual void CreateCohortSelectionMethodFor(ISpecies species,
                                                              IList<ushort> ages,
                                                              IList<AgeRange> ranges)
        {
            cohortSelector[species] = new SpecificAgesCohortSelector(ages, ranges).SelectCohorts;
        }

        /// <summary>
        /// Creates and stores the cohort selection method for a particular
        /// species based on lists of specific ages and age ranges for single repeat.
        /// </summary>
        /// <remarks>
        /// Derived classes can override this method to perform special
        /// handling of ages and ranges (for example, percentages for partial
        /// harvesting in biomass extensions).
        /// </remarks>
        protected virtual void CreateAdditionalCohortSelectionMethodFor(ISpecies species,
                                                              IList<ushort> ages,
                                                              IList<AgeRange> ranges)
        {
            additionalCohortSelector[species] = new SpecificAgesCohortSelector(ages, ranges).SelectCohorts;
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Validates a cohort age or age range against previous ages and
        /// ranges.
        /// </summary>
        /// <param name="ageOrRange">
        /// The age or age range that's being validated.
        /// </param>
        /// <param name="ages">
        /// List of previous ages.
        /// </param>
        /// <param name="ranges">
        /// List of previous ranges.
        /// </param>
        /// <remarks>
        /// If the age or range is validated, it is added to the corresponding
        /// list.
        /// </remarks>
        protected void ValidateAgeOrRange(InputValue<AgeRange> ageOrRange,
                                          List<ushort> ages,
                                          List<AgeRange> ranges)
        {
            if (ageOrRange.String.Contains("-"))
            {
                AgeRange range = ageOrRange.Actual;

                //  Does the range contain any individual ages?
                foreach (ushort age in ages)
                {
                    if (range.Contains(age))
                        throw new InputValueException(ageOrRange.String,
                                                      "The range {0} contains the age {1}",
                                                      ageOrRange.String, age);
                }

                //  Does the range overlap any previous ranges?
                foreach (AgeRange previousRange in ranges)
                {
                    if (range.Overlaps(previousRange))
                        throw new InputValueException(ageOrRange.String,
                                                      "The range {0} overlaps the range {1}-{2}",
                                                      ageOrRange.String, previousRange.Start, previousRange.End);
                }

                ranges.Add(range);
            }
            else
            {
                ushort age = ageOrRange.Actual.Start;

                //  Does the age match any of the previous ages?
                foreach (ushort previousAge in ages)
                {
                    if (age == previousAge)
                        throw new InputValueException(ageOrRange.String,
                                                      "The age {0} appears more than once",
                                                      ageOrRange.String);
                }

                //  Is the age in any of the previous ranges?
                foreach (AgeRange previousRange in ranges)
                {
                    if (previousRange.Contains(age))
                        throw new InputValueException(ageOrRange.String,
                                                      "The age {0} lies within the range {1}-{2}",
                                                      ageOrRange.String, previousRange.Start, previousRange.End);
                }

                ages.Add(age);
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Reads the optional "Plant" parameter which specifies one or more
        /// tree species to plant at a site.
        /// </summary>
        protected Planting.SpeciesList ReadSpeciesToPlant()
        {
            InputVar<List<ISpecies>> plant = new InputVar<List<ISpecies>>(ParameterNames.Plant, ReadSpeciesList);
            if (ReadOptionalVar(plant))
                return new Planting.SpeciesList(plant.Value.Actual, speciesDataset);
            else
                return null;
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Reads a list of species names from the current input line.
        /// </summary>
        public InputValue<List<ISpecies>> ReadSpeciesList(StringReader currentLine,
                                                          out int index)
        {
            List<string> speciesNames = new List<string>();
            List<ISpecies> speciesList = new List<ISpecies>();

            TextReader.SkipWhitespace(currentLine);
            index = currentLine.Index;
            while (currentLine.Peek() != -1)
            {
                ISpecies species = ReadAndValidateSpeciesName(currentLine);
                if (speciesNames.Contains(species.Name))
                    throw new InputValueException(speciesName.Value.String,
                                                  "The species {0} appears more than once.", species.Name);
                speciesNames.Add(species.Name);
                speciesList.Add(species);

                TextReader.SkipWhitespace(currentLine);
            }
            if (speciesNames.Count == 0)
                throw new InputValueException(); // Missing value

            return new InputValue<List<ISpecies>>(speciesList,
                                                  string.Join(" ", speciesNames.ToArray()));
        }
    }
}