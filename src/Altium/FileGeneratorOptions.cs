namespace Altium
{
    public class FileGeneratorOptions
    {
        /// <summary>
        /// Желаемая длина итогового файла (Б).
        /// </summary>
        public long DesiredFileLength { get; set; }

        /// <summary>
        /// Размер словаря, из которого набираются строки при генерации файла (количество уникальных слов).
        /// </summary>
        public int VocabularyLength { get; set; } = 1000;

        /// <summary>
        /// Макс. длина слова в словаре.
        /// </summary>
        public int MaxWordLength { get; set; } = 16;

        /// <summary>
        /// Шанс переиспользовать ранее сгенерированную строку.
        /// </summary>
        public int ReuseLineChance = 5;

        /// <summary>
        /// Cache строк. По условию задания в файле должны присутствовать повторяющиеся строки.
        /// </summary>
        public int LineCacheSize { get; set; } = 1000;

        /// <summary>
        /// Размер батча при генерации файла (в количестве строк).
        /// Запись в файл происходит батчами заданного размера.
        /// </summary>
        public int BatchSize { get; set; } = 1000000;

        /// <summary>
        /// Максимальное значение числа в строке.
        /// </summary>
        public int NumberMaxValue { get; set; } = 1024;

        /// <summary>
        /// Максимальная длина символьной части строки (в количестве символов).
        /// </summary>
        public int TextMaxLength { get; set; } = 1024;
    }
}