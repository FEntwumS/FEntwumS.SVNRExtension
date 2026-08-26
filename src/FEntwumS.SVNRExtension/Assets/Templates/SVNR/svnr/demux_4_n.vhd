
library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

-- Uncomment the following library declaration if using
-- arithmetic functions with Signed or Unsigned values
--use IEEE.NUMERIC_STD.ALL;

-- Uncomment the following library declaration if instantiating
-- any Xilinx leaf cells in this code.
--library UNISIM;
--use UNISIM.VComponents.all;

entity demux_4_n is
    Generic ( size : integer := 1);
    Port ( sel : in STD_LOGIC_VECTOR (1 downto 0);
           data0 : out STD_LOGIC_VECTOR (size-1 downto 0);
           data1 : out STD_LOGIC_VECTOR (size-1 downto 0);
           data2 : out STD_LOGIC_VECTOR (size-1 downto 0);
           data3 : out STD_LOGIC_VECTOR (size-1 downto 0);
           input_data : in STD_LOGIC_VECTOR (size-1 downto 0));
end demux_4_n;

architecture Behavioral of demux_4_n is

begin
    data0 <= input_data when sel = "00" else (others => '0');
    data1 <= input_data when sel = "01" else (others => '0');
    data2 <= input_data when sel = "10" else (others => '0');
    data3 <= input_data when sel = "11" else (others => '0');
end Behavioral;
